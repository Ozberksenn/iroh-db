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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // BookingStatus enum'unu veritabanındaki string (character varying) alanıyla eşleştiriyoruz.
        modelBuilder.Entity<Booking>()
            .Property(b => b.status)
            .HasConversion<string>();
    }
}
