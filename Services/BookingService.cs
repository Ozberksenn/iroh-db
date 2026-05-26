using Iroh.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public class BookingService
    {
        private readonly AppDbContext _context;
        public BookingService(AppDbContext context)
        {
            _context = context;
        }

        public List<Booking> GetAll()
        {
            // .Include(b => b.table) ekleyerek masanın bilgilerini (adını vb.) de getiriyoruz.
            return _context.Booking
                .Include(b => b.table)
                .Include(b => b.customer)
                .ToList();
        }

        public Booking Create(Booking booking)
        {
            _context.Booking.Add(booking);
            _context.SaveChanges();
            return booking;
        }
    }
}
