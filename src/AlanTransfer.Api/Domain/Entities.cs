namespace AlanTransfer.Api.Domain;

// Ortak iş kaydının (iş defteri) yaşam döngüsü durumları.
// Sayısal kodlar yerine açık, herkesin anlayacağı adlar kullanılır.
public enum ReservationStatus
{
    Created = 0,     // Rezervasyon oluştu
    Paid = 1,        // Ödeme alındı
    InPool = 2,      // Havuza bırakıldı
    Assigned = 3,    // Belirli bir sürücüye doğrudan atandı
    Accepted = 4,    // Sürücü işi kabul etti
    EnRoute = 5,     // Alış noktasına gidiliyor / yolculuk başladı
    Completed = 6,   // Transfer tamamlandı
    Cancelled = 7
}

// Bir işin havuzda mı yoksa doğrudan atama ile mi ilerlediğini belirtir.
// Aynı iş aynı anda hem havuzda hem doğrudan atamada bulunamaz.
public enum AssignmentType
{
    None = 0,
    Direct = 1,
    Pool = 2
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Operasyon = "Operasyon";
    public const string Muhasebe = "Muhasebe";
    public const string Acente = "Acente";
    public const string Surucu = "Surucu";
}

public class Agency
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Driver> Drivers { get; set; } = new();
}

public class Driver
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Plate { get; set; } = "";
    public int Capacity { get; set; }
    public bool IsActive { get; set; } = true;
    // Sürücü bir acenteye bağlı olabilir ya da bağımsız olabilir.
    public int? AgencyId { get; set; }
    public Agency? Agency { get; set; }
}

public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    // Demo amaçlı düz metin; gerçek sistemde hash + salt kullanılır.
    public string Password { get; set; } = "";
    public string Role { get; set; } = Roles.Operasyon;
    public int? AgencyId { get; set; }
    public int? DriverId { get; set; }
}

// Ortak iş kaydı: tek rezervasyon, tüm kanallarda aynı kayıt üzerinden ilerler.
public class Reservation
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string PickUp { get; set; } = "";
    public string DropOff { get; set; } = "";
    public DateTime PickupAt { get; set; }
    public int PaxCount { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "TRY";
    public ReservationStatus Status { get; set; } = ReservationStatus.Created;
    public AssignmentType AssignmentType { get; set; } = AssignmentType.None;

    // Kaydın hangi kanaldan geldiği: Web, Acente, Portal.
    public string SourceChannel { get; set; } = "Web";

    // İşi oluşturan acente (varsa). Acente yalnızca kendi kayıtlarını görür.
    public int? AgencyId { get; set; }
    // İşi üstlenen sürücü (atandıysa / kabul edildiyse).
    public int? DriverId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Eş zamanlılık kontrolü: iki sürücünün aynı işi aynı anda kabul etmesini
    // veritabanı seviyesinde engeller (SQL Server rowversion / optimistic concurrency).
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<ReservationHistory> History { get; set; } = new();
}

// Denetim izi: kritik işlemlerde kim, ne zaman, neyi değiştirdi.
public class ReservationHistory
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public string Action { get; set; } = "";
    public string Detail { get; set; } = "";
    public string ByUser { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}
