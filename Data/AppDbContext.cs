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
    public DbSet<Package> Packages { get; set; }

    public DbSet<Child> Children { get; set; }

    // Cüzdan + iki defter (docs/wallet-redesign.md). Purchase ekosistemini kademeli ikame eder.
    public DbSet<Wallet> Wallets { get; set; }
    public DbSet<TimeLedgerEntry> TimeLedger { get; set; }
    public DbSet<CashLedgerEntry> CashLedger { get; set; }
    // NOT: UnifiedSearchResultDto DbSet/HasNoKey DEĞİL — LINQ projeksiyon hedefi (FromSql kaldırıldı).

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

        // F1.3: Para/oran alanları numeric(12,2) — int kırpma yok, defterlerle tutarlı.
        modelBuilder.Entity<Company>().Property(c => c.FirstHourPrice).HasPrecision(12, 2);
        modelBuilder.Entity<Company>().Property(c => c.AdditionalHalfHourPrice).HasPrecision(12, 2);
        modelBuilder.Entity<Package>().Property(p => p.Price).HasPrecision(12, 2);
        modelBuilder.Entity<Package>().Property(p => p.Hours).HasPrecision(12, 2);
        modelBuilder.Entity<Booking>().Property(b => b.Price).HasPrecision(12, 2);
        modelBuilder.Entity<Wallet>().Property(w => w.CashBalance).HasPrecision(12, 2);
        modelBuilder.Entity<CashLedgerEntry>().Property(e => e.AmountDelta).HasPrecision(12, 2);
    }
}
