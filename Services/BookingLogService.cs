using Iroh.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public class BookingLogService
    {
        private readonly AppDbContext _context;
        public BookingLogService(AppDbContext context)
        {
            _context = context;
        }

        public List<BookingLog> GetAll()
        {
            return _context.BookingLog
                .Include(b => b.booking)
                .OrderByDescending(b => b.time)
                .ToList();
        }

        public BookingLog Create(BookingLog log)
        {
            _context.BookingLog.Add(log);
            _context.SaveChanges();
            return log;
        }
    }
}
