using Iroh.Models.Entities;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Table> Table { get; set; }
    public DbSet<Company> Company { get; set; }
    public DbSet<Customer> Customer { get; set; }
    public DbSet<Booking> Booking { get; set; }
    public DbSet<BookingLog> BookingLog { get; set; }
    public DbSet<User> User { get; set; }
    public DbSet<Purchase> Purchase { get; set; }
    public DbSet<PurchaseBooking> purchaseBookings { get; set; }
    public DbSet<PurchasePayment> purchasePayments { get; set; }

    public DbSet<Child> Children { get; set; }
    public DbSet<Iroh.Models.DTOs.Child.UnifiedSearchResultDto> UnifiedSearchResults { get; set; }
    public DbSet<Iroh.Models.DTOs.Purchase.CustomerPurchaseResultDto> CustomerPurchaseResults { get; set; }
    public DbSet<Iroh.Models.DTOs.Purchase.PurchaseBookingResultDto> PurchaseBookingResults { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Iroh.Models.DTOs.Child.UnifiedSearchResultDto>().HasNoKey();
        modelBuilder.Entity<Iroh.Models.DTOs.Purchase.CustomerPurchaseResultDto>().HasNoKey();
        modelBuilder.Entity<Iroh.Models.DTOs.Purchase.PurchaseBookingResultDto>().HasNoKey();

        // BookingStatus enum'unu veritabanındaki string (character varying) alanıyla eşleştiriyoruz.
        modelBuilder.Entity<Booking>()
            .Property(b => b.status)
            .HasConversion<string>();

        // BookingLogType enum'unu veritabanındaki string (character varying) alanıyla eşleştiriyoruz.
        modelBuilder.Entity<BookingLog>()
            .Property(bl => bl.type)
            .HasConversion<string>();
    }
}
