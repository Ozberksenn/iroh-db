using Iroh.Models.Entities;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Define your DbSet properties here
    public DbSet<Table> Table { get; set; }
    public DbSet<Company> Company { get; set; }
    public DbSet<Customer> Customer { get; set; }
    public DbSet<Booking> Booking { get; set; }
}