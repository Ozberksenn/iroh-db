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

        public async Task<List<BookingLog>> GetAll() =>
            await _context.BookingLog
                .Include(b => b.booking)
                .OrderByDescending(b => b.time)
                .ToListAsync();

        public async Task<BookingLog> Create(BookingLog log)
        {
            _context.BookingLog.Add(log);
            await _context.SaveChangesAsync();
            return log;
        }

        public async Task<BookingLog?> GetById(int id) =>
            await _context.BookingLog.FindAsync(id);

        public async Task<BookingLog> Update(BookingLog log)
        {
            _context.BookingLog.Update(log);
            await _context.SaveChangesAsync();
            return log;
        }
    }
}
