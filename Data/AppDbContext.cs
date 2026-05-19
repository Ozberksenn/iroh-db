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

    public DbSet<Child> child { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // BookingStatus enum'unu veritabanındaki string (character varying) alanıyla eşleştiriyoruz.
        modelBuilder.Entity<Booking>()
            .Property(b => b.status)
            .HasConversion<string>();
    }
}
