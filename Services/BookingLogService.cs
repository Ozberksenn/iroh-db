using Iroh.Exceptions;
using Iroh.Models.DTOs.BookingLog;
using Iroh.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public interface IBookingLogService
    {
        Task<List<BookingLog>> GetAll();
        Task<BookingLog> Create(BookingLog log);
        Task<BookingLog?> GetById(int id);
        Task<BookingLog> Update(int id, BookingLogUpdateDto dto);
    }

    public class BookingLogService : IBookingLogService
    {
        private readonly AppDbContext _context;
        public BookingLogService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<BookingLog>> GetAll() =>
            await _context.BookingLogs
                .AsNoTracking()
                .Include(b => b.Booking)
                .OrderByDescending(b => b.Time)
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

            log.BookingId = dto.BookingId;
            log.Time = dto.Time;
            log.Type = dto.Type;
            log.UserId = dto.UserId;

            await _context.SaveChangesAsync();
            return log;
        }
    }
}
