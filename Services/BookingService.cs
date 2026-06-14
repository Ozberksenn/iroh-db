using Iroh.Models.DTOs.Booking;
using Iroh.Models.DTOs.Common;
using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Iroh.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public interface IBookingService
    {
        Task<PagedResult<BookingListItemDto>> GetBookings(
            int page, int size, string[]? status, string? name, string? mail,
            int? customerId, int? childId, DateTime? startTime, DateTime? endTime, int? tableId);
        Task<Booking?> GetById(int id);
        Task<Booking> Create(Booking booking);
        Task<Booking> Update(int id, BookingUpdateDto updatedBooking);
    }

    public class BookingService : IBookingService
    {
        private readonly AppDbContext _context;
        public BookingService(AppDbContext context)
        {
            _context = context;
        }

        // usp_get_bookings paritesi: sayfalı + filtreli liste; table/customer iç içe DTO; ORDER BY id DESC.
        public async Task<PagedResult<BookingListItemDto>> GetBookings(
            int page, int size, string[]? status, string? name, string? mail,
            int? customerId, int? childId, DateTime? startTime, DateTime? endTime, int? tableId)
        {
            // NOT: p_mail proc imzasında var ama gövdesinde kullanılmıyor (no-op) — parite için yok sayıldı.
            var query = _context.Bookings.AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                var pattern = $"%{name}%";
                query = query.Where(b => EF.Functions.ILike(
                    (b.Child != null ? b.Child.Name : "") + " " +
                    (b.Child != null && b.Child.Parent != null ? b.Child.Parent.Name : "") + " " +
                    (b.Child != null && b.Child.Parent != null ? (b.Child.Parent.LastName ?? "") : "") + " " +
                    (b.Child != null && b.Child.Parent != null ? (b.Child.Parent.Phone ?? "") : "") + " " +
                    (b.Table != null ? (b.Table.Name ?? "") : "") + " " +
                    (b.Note ?? ""),
                    pattern));
            }

            if (status != null && status.Length > 0)
            {
                var statusEnums = status
                    .Select(s => Enum.TryParse<BookingStatus>(s, true, out var e) ? (BookingStatus?)e : null)
                    .Where(e => e.HasValue)
                    .Select(e => e!.Value)
                    .ToList();
                if (statusEnums.Count > 0)
                {
                    query = query.Where(b => statusEnums.Contains(b.Status));
                }
            }

            if (customerId.HasValue)
                query = query.Where(b => b.Child != null && b.Child.ParentId == customerId.Value);
            if (childId.HasValue)
                query = query.Where(b => b.ChildId == childId.Value);
            if (startTime.HasValue)
                query = query.Where(b => b.StartTime >= startTime.Value);
            if (endTime.HasValue)
                query = query.Where(b => b.StartTime <= endTime.Value);
            if (tableId.HasValue)
                query = query.Where(b => b.TableId == tableId.Value);

            var totalSize = await query.CountAsync();

            IQueryable<Booking> ordered = query.OrderByDescending(b => b.Id);
            if (page != -1)
                ordered = ordered.Skip((page - 1) * size).Take(size);

            var items = await ordered.Select(b => new BookingListItemDto
            {
                Id = b.Id,
                Table = b.TableId != null
                    ? new BookingTableDto { Id = b.Table!.Id, Name = b.Table.Name }
                    : null,
                Customer = b.ChildId != null
                    ? new BookingCustomerDto
                    {
                        ChildId = b.Child!.Id,
                        Name = b.Child.Name,
                        ParentId = b.Child.Parent != null ? b.Child.Parent.Id : (int?)null,
                        ParentName = b.Child.Parent != null ? b.Child.Parent.Name : null,
                        ParentLastName = b.Child.Parent != null ? b.Child.Parent.LastName : null,
                        Phone = b.Child.Parent != null ? b.Child.Parent.Phone : null
                    }
                    : null,
                Price = b.Price,
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                SubscriptionStartTime = b.SubscriptionStartTime,
                SubscriptionEndTime = b.SubscriptionEndTime,
                Status = b.Status.ToString(),
                Note = b.Note
            }).ToListAsync();

            return new PagedResult<BookingListItemDto>
            {
                Items = items,
                Page = page,
                Size = size,
                TotalSize = totalSize,
                TotalPages = page == -1 ? 1 : (int)Math.Ceiling((double)totalSize / size)
            };
        }

        public async Task<Booking?> GetById(int id) =>
            await _context.Bookings
                .AsNoTracking()
                .Include(b => b.Table)
                .Include(b => b.Child)
                    .ThenInclude(c => c.Parent)
                .FirstOrDefaultAsync(b => b.Id == id);

        public async Task<Booking> Create(Booking booking)
        {
            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();
            return booking;
        }

        public async Task<Booking> Update(int id, BookingUpdateDto updatedBooking)
        {
            var existingBooking = await _context.Bookings.FindAsync(id);
            if (existingBooking == null)
            {
                throw new NotFoundException("Kayıt bulunamadı");
            }

            existingBooking.TableId = updatedBooking.TableId ?? existingBooking.TableId;
            existingBooking.StartTime = updatedBooking.StartTime ?? existingBooking.StartTime;
            existingBooking.EndTime = updatedBooking.EndTime ?? existingBooking.EndTime;
            existingBooking.Status = updatedBooking.Status;
            existingBooking.Price = updatedBooking.Price ?? existingBooking.Price;
            existingBooking.ChildId = updatedBooking.ChildId ?? existingBooking.ChildId;
            existingBooking.Note = updatedBooking.Note ?? existingBooking.Note;
            existingBooking.SubscriptionStartTime = updatedBooking.SubscriptionStartTime ?? existingBooking.SubscriptionStartTime;
            existingBooking.SubscriptionEndTime = updatedBooking.SubscriptionEndTime ?? existingBooking.SubscriptionEndTime;

            // Eğer bir paket (Purchase) kullanılıyorsa, onu da bağla (usp_update_booking logic)
            if (updatedBooking.PurchaseId.HasValue)
            {
                var exists = await _context.PurchaseBookings.AnyAsync(pb => pb.BookingId == existingBooking.Id && pb.PurchaseId == updatedBooking.PurchaseId.Value);
                if (!exists)
                {
                    _context.PurchaseBookings.Add(new PurchaseBooking
                    {
                        BookingId = existingBooking.Id,
                        PurchaseId = updatedBooking.PurchaseId.Value
                    });
                }
            }

            await _context.SaveChangesAsync();
            return existingBooking;
        }
    }
}
