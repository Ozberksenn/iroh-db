using Iroh.Exceptions;
using Iroh.Models.DTOs.BookingLog;
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
            await _context.BookingLogs
                .AsNoTracking()
                .Include(b => b.booking)
                .OrderByDescending(b => b.time)
                .ToListAsync();

        public async Task<BookingLog> Create(BookingLog log)
        {
            _context.BookingLogs.Add(log);
            await _context.SaveChangesAsync();
            return log;
        }

        public async Task<BookingLog?> GetById(int id) =>
            await _context.BookingLogs.FindAsync(id);

        public async Task<BookingLog> Update(int id, BookingLogUpdateDto dto)
        {
            var log = await GetById(id)
                ?? throw new NotFoundException("Kayıt bulunamadı");

            log.bookingId = dto.bookingId;
            log.time = dto.time;
            log.type = dto.type;
            log.userId = dto.userId;

            await _context.SaveChangesAsync();
            return log;
        }
    }
}
