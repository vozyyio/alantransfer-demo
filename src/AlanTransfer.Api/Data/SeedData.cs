using AlanTransfer.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlanTransfer.Api.Data;

public static class SeedData
{
    public static async Task EnsureSeededAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Users.AnyAsync()) return;

        var acenteA = new Agency { Name = "Akdeniz Turizm" };
        var acenteB = new Agency { Name = "Rivyera Travel" };
        db.Agencies.AddRange(acenteA, acenteB);
        await db.SaveChangesAsync();

        var sofor1 = new Driver { Name = "Ahmet Y.", Plate = "07 ABC 123", Capacity = 4, AgencyId = null };
        var sofor2 = new Driver { Name = "Mehmet K.", Plate = "07 DEF 456", Capacity = 6, AgencyId = null };
        var soforAcente = new Driver { Name = "Hasan T.", Plate = "07 GHI 789", Capacity = 4, AgencyId = acenteA.Id };
        db.Drivers.AddRange(sofor1, sofor2, soforAcente);
        await db.SaveChangesAsync();

        db.Users.AddRange(
            new AppUser { Username = "admin", Password = "1234", Role = Roles.Admin },
            new AppUser { Username = "operasyon", Password = "1234", Role = Roles.Operasyon },
            new AppUser { Username = "muhasebe", Password = "1234", Role = Roles.Muhasebe },
            new AppUser { Username = "acenteA", Password = "1234", Role = Roles.Acente, AgencyId = acenteA.Id },
            new AppUser { Username = "acenteB", Password = "1234", Role = Roles.Acente, AgencyId = acenteB.Id },
            new AppUser { Username = "sofor1", Password = "1234", Role = Roles.Surucu, DriverId = sofor1.Id },
            new AppUser { Username = "sofor2", Password = "1234", Role = Roles.Surucu, DriverId = sofor2.Id }
        );
        await db.SaveChangesAsync();

        // Örnek bir web rezervasyonu (ortak iş kaydı).
        var r = new Reservation
        {
            Code = "ALT-100001",
            PickUp = "İstanbul Havalimanı (IST)",
            DropOff = "Beşiktaş - Otel",
            PickupAt = DateTime.UtcNow.AddDays(1).Date.AddHours(10),
            PaxCount = 3,
            Price = 1200m,
            Currency = "TRY",
            Status = ReservationStatus.Paid,
            SourceChannel = "Web"
        };
        r.History.Add(new ReservationHistory { Action = "Oluşturuldu", Detail = "Web kanalından rezervasyon", ByUser = "web" });
        r.History.Add(new ReservationHistory { Action = "Ödeme", Detail = "Kart ile ödeme başarılı", ByUser = "web" });
        db.Reservations.Add(r);
        await db.SaveChangesAsync();
    }
}
