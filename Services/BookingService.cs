
using Iroh.Models.Entities;

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
            return _context.Booking.ToList();
        }
    }
}