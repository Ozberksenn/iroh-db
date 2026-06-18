using Iroh.Models.Entities;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Table> Tables { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<BookingLog> BookingLogs { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Purchase> Purchases { get; set; }
    public DbSet<PurchaseBooking> PurchaseBookings { get; set; }
    public DbSet<PurchasePayment> PurchasePayments { get; set; }
    public DbSet<Package> Packages { get; set; }

    public DbSet<Child> Children { get; set; }

    // Cüzdan + iki defter (docs/wallet-redesign.md). Purchase ekosistemini kademeli ikame eder.
    public DbSet<Wallet> Wallets { get; set; }
    public DbSet<TimeLedgerEntry> TimeLedger { get; set; }
    public DbSet<CashLedgerEntry> CashLedger { get; set; }
    // NOT: UnifiedSearchResultDto / CustomerPurchaseResultDto / PurchaseBookingResultDto artık
    // DbSet/HasNoKey DEĞİL — LINQ projeksiyon hedefi olarak kullanılıyorlar (FromSql kaldırıldı).

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // BookingStatus enum'unu veritabanındaki string (character varying) alanıyla eşleştiriyoruz.
        modelBuilder.Entity<Booking>()
            .Property(b => b.Status)
            .HasConversion<string>();

        // BookingLogType enum'unu veritabanındaki string (character varying) alanıyla eşleştiriyoruz.
        modelBuilder.Entity<BookingLog>()
            .Property(bl => bl.Type)
            .HasConversion<string>();

        // Defter tipleri de DB'de string (character varying) tutulur — BookingStatus ile aynı kalıp.
        modelBuilder.Entity<TimeLedgerEntry>()
            .Property(e => e.Type)
            .HasConversion<string>();

        modelBuilder.Entity<CashLedgerEntry>()
            .Property(e => e.Type)
            .HasConversion<string>();
    }
}
