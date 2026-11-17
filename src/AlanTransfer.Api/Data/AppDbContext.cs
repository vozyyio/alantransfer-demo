using AlanTransfer.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlanTransfer.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationHistory> ReservationHistory => Set<ReservationHistory>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Reservation>().HasIndex(r => r.Code).IsUnique();
        b.Entity<Reservation>().Property(r => r.Price).HasColumnType("decimal(18,2)");
        b.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();
    }
}
