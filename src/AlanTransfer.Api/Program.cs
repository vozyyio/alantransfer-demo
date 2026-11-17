using System.Security.Claims;
using System.Text;
using AlanTransfer.Api.Auth;
using AlanTransfer.Api.Data;
using AlanTransfer.Api.Domain;
using AlanTransfer.Api.Dtos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var conn = builder.Configuration.GetConnectionString("Default")
    ?? "Server=db;Database=AlanTransferDemo;User Id=sa;Password=Your_strong_Pass123;TrustServerCertificate=True;";

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(conn));
builder.Services.AddSingleton<JwtService>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? "demo-super-secret-key-change-in-prod-123456789";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AlanTransfer Demo API", Version = "v1" });
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { scheme, Array.Empty<string>() } });
});

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();

// Veritabanını hazırla ve örnek verileri yükle.
// SQL Server konteyneri geç açılabileceği için birkaç kez dener.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    for (var attempt = 1; attempt <= 15; attempt++)
    {
        try { await SeedData.EnsureSeededAsync(db); break; }
        catch (Exception ex)
        {
            Console.WriteLine($"DB hazır değil (deneme {attempt}/15): {ex.Message}");
            await Task.Delay(4000);
        }
    }
}

static ReservationDto Map(Reservation r) => new(
    r.Id, r.Code, r.PickUp, r.DropOff, r.PickupAt, r.PaxCount, r.Price, r.Currency,
    r.Status.ToString(), r.AssignmentType.ToString(), r.SourceChannel, r.AgencyId, r.DriverId);

static int? AgencyIdOf(ClaimsPrincipal u) =>
    int.TryParse(u.FindFirstValue("agencyId"), out var v) ? v : null;
static int? DriverIdOf(ClaimsPrincipal u) =>
    int.TryParse(u.FindFirstValue("driverId"), out var v) ? v : null;

// ---- Auth ----
app.MapPost("/api/v1/auth/login", async (LoginRequest req, AppDbContext db, JwtService jwt) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username && u.Password == req.Password);
    if (user is null) return Results.Unauthorized();
    return Results.Ok(new LoginResponse(jwt.CreateToken(user), user.Role, user.Username));
});

// ---- Rezervasyon oluşturma (Web / Acente / Operasyon) — tek ortak kayıt ----
app.MapPost("/api/v1/reservations", async (CreateReservationRequest req, ClaimsPrincipal user, AppDbContext db) =>
{
    var next = await db.Reservations.CountAsync() + 100002;
    var r = new Reservation
    {
        Code = $"ALT-{next}",
        PickUp = req.PickUp,
        DropOff = req.DropOff,
        PickupAt = req.PickupAt,
        PaxCount = req.PaxCount,
        Price = req.Price,
        Currency = string.IsNullOrWhiteSpace(req.Currency) ? "TRY" : req.Currency,
        Status = ReservationStatus.Paid,
        SourceChannel = user.IsInRole(Roles.Acente) ? "Acente" : "Portal",
        AgencyId = AgencyIdOf(user)
    };
    r.History.Add(new ReservationHistory
    {
        Action = "Oluşturuldu",
        Detail = $"{r.SourceChannel} kanalından rezervasyon",
        ByUser = user.Identity?.Name ?? "?"
    });
    db.Reservations.Add(r);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/reservations/{r.Id}", Map(r));
}).RequireAuthorization();

// ---- Listeleme (role göre filtrelenir) ----
app.MapGet("/api/v1/reservations", async (ClaimsPrincipal user, AppDbContext db) =>
{
    IQueryable<Reservation> q = db.Reservations.AsNoTracking().OrderByDescending(r => r.Id);

    if (user.IsInRole(Roles.Acente))
    {
        var aid = AgencyIdOf(user);
        q = q.Where(r => r.AgencyId == aid); // acente yalnızca kendi kayıtları
    }
    else if (user.IsInRole(Roles.Surucu))
    {
        var did = DriverIdOf(user);
        q = q.Where(r => r.DriverId == did); // sürücü yalnızca kendi işleri
    }
    // Admin / Operasyon / Muhasebe: tümü

    var list = await q.Select(r => Map(r)).ToListAsync();
    return Results.Ok(list);
}).RequireAuthorization();

// ---- Tekil kayıt (kaynak bazlı yetki kontrolü) ----
app.MapGet("/api/v1/reservations/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
{
    var r = await db.Reservations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (r is null) return Results.NotFound();

    if (user.IsInRole(Roles.Acente) && r.AgencyId != AgencyIdOf(user))
        return Results.Forbid(); // başka acentenin kaydı
    if (user.IsInRole(Roles.Surucu) && r.DriverId != DriverIdOf(user) && r.Status != ReservationStatus.InPool)
        return Results.Forbid(); // sürücünün olmayan ve havuzda olmayan iş

    return Results.Ok(Map(r));
}).RequireAuthorization();

// ---- Havuza bırak (Operasyon) ----
app.MapPost("/api/v1/reservations/{id:int}/send-to-pool", async (int id, ClaimsPrincipal user, AppDbContext db) =>
{
    var r = await db.Reservations.FindAsync(id);
    if (r is null) return Results.NotFound();
    if (r.Status is ReservationStatus.Accepted or ReservationStatus.EnRoute or ReservationStatus.Completed)
        return Results.Conflict(new { message = "İş zaten bir sürücüye bağlı." });

    r.Status = ReservationStatus.InPool;
    r.AssignmentType = AssignmentType.Pool;
    r.DriverId = null; // aynı iş hem havuzda hem direkt atamada olamaz
    db.ReservationHistory.Add(new ReservationHistory { ReservationId = r.Id, Action = "Havuza bırakıldı", ByUser = user.Identity?.Name ?? "?" });
    await db.SaveChangesAsync();
    return Results.Ok(Map(r));
}).RequireAuthorization(p => p.RequireRole(Roles.Operasyon, Roles.Admin));

// ---- Doğrudan atama (Operasyon) ----
app.MapPost("/api/v1/reservations/{id:int}/assign", async (int id, AssignRequest req, ClaimsPrincipal user, AppDbContext db) =>
{
    var r = await db.Reservations.FindAsync(id);
    if (r is null) return Results.NotFound();
    var driver = await db.Drivers.FindAsync(req.DriverId);
    if (driver is null) return Results.BadRequest(new { message = "Sürücü bulunamadı." });

    r.Status = ReservationStatus.Assigned;
    r.AssignmentType = AssignmentType.Direct;
    r.DriverId = req.DriverId; // yalnızca bu sürücü görür
    db.ReservationHistory.Add(new ReservationHistory { ReservationId = r.Id, Action = "Doğrudan atandı", Detail = driver.Name, ByUser = user.Identity?.Name ?? "?" });
    await db.SaveChangesAsync();
    return Results.Ok(Map(r));
}).RequireAuthorization(p => p.RequireRole(Roles.Operasyon, Roles.Admin));

// ---- Havuzdaki uygun işler (Sürücü) ----
app.MapGet("/api/v1/pool", async (ClaimsPrincipal user, AppDbContext db) =>
{
    var list = await db.Reservations.AsNoTracking()
        .Where(r => r.Status == ReservationStatus.InPool)
        .OrderBy(r => r.PickupAt)
        .Select(r => Map(r)).ToListAsync();
    return Results.Ok(list);
}).RequireAuthorization(p => p.RequireRole(Roles.Surucu));

// ---- İşi kabul et (Sürücü) — EŞ ZAMANLILIK KONTROLÜ ----
// İki sürücü aynı işi aynı anda kabul etmeye çalışırsa yalnızca biri alır.
// RowVersion (SQL Server rowversion) sayesinde ikinci istek 409 Conflict alır.
app.MapPost("/api/v1/reservations/{id:int}/accept", async (int id, ClaimsPrincipal user, AppDbContext db) =>
{
    var driverId = DriverIdOf(user);
    if (driverId is null) return Results.Forbid();

    var r = await db.Reservations.FindAsync(id);
    if (r is null) return Results.NotFound();

    var canTakeFromPool = r.Status == ReservationStatus.InPool;
    var isOwnDirect = r.Status == ReservationStatus.Assigned && r.DriverId == driverId;
    if (!canTakeFromPool && !isOwnDirect)
        return Results.Conflict(new { message = "Bu iş artık alınamaz (başka sürücü almış ya da uygun durumda değil)." });

    r.DriverId = driverId;
    r.Status = ReservationStatus.Accepted;
    if (r.AssignmentType == AssignmentType.None) r.AssignmentType = AssignmentType.Pool;
    db.ReservationHistory.Add(new ReservationHistory { ReservationId = r.Id, Action = "Kabul edildi", Detail = $"Sürücü #{driverId}", ByUser = user.Identity?.Name ?? "?" });

    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        // Aynı anda başka bir sürücü işi aldı.
        return Results.Conflict(new { message = "İş aynı anda başka bir sürücü tarafından alındı." });
    }
    return Results.Ok(Map(r));
}).RequireAuthorization(p => p.RequireRole(Roles.Surucu));

// ---- Transfer durumunu güncelle (Sürücü) ----
app.MapPost("/api/v1/reservations/{id:int}/status", async (int id, StatusRequest req, ClaimsPrincipal user, AppDbContext db) =>
{
    var r = await db.Reservations.FindAsync(id);
    if (r is null) return Results.NotFound();
    if (r.DriverId != DriverIdOf(user)) return Results.Forbid();

    r.Status = req.Step switch
    {
        "EnRoute" => ReservationStatus.EnRoute,
        "Completed" => ReservationStatus.Completed,
        "Cancelled" => ReservationStatus.Cancelled,
        _ => r.Status
    };
    db.ReservationHistory.Add(new ReservationHistory { ReservationId = r.Id, Action = "Durum", Detail = req.Step, ByUser = user.Identity?.Name ?? "?" });
    await db.SaveChangesAsync();
    return Results.Ok(Map(r));
}).RequireAuthorization(p => p.RequireRole(Roles.Surucu));

app.Run();
