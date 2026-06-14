using Iroh.Models.DTOs.Booking;
using Iroh.Models.DTOs.Common;
using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Iroh.Exceptions;
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
                    (b.child != null ? b.child.name : "") + " " +
                    (b.child != null && b.child.parent != null ? b.child.parent.name : "") + " " +
                    (b.child != null && b.child.parent != null ? (b.child.parent.lastName ?? "") : "") + " " +
                    (b.child != null && b.child.parent != null ? (b.child.parent.phone ?? "") : "") + " " +
                    (b.table != null ? (b.table.name ?? "") : "") + " " +
                    (b.note ?? ""),
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
                    query = query.Where(b => statusEnums.Contains(b.status));
                }
            }

            if (customerId.HasValue)
                query = query.Where(b => b.child != null && b.child.parentId == customerId.Value);
            if (childId.HasValue)
                query = query.Where(b => b.childId == childId.Value);
            if (startTime.HasValue)
                query = query.Where(b => b.startTime >= startTime.Value);
            if (endTime.HasValue)
                query = query.Where(b => b.startTime <= endTime.Value);
            if (tableId.HasValue)
                query = query.Where(b => b.tableId == tableId.Value);

            var totalSize = await query.CountAsync();

            IQueryable<Booking> ordered = query.OrderByDescending(b => b.id);
            if (page != -1)
                ordered = ordered.Skip((page - 1) * size).Take(size);

            var items = await ordered.Select(b => new BookingListItemDto
            {
                id = b.id,
                table = b.tableId != null
                    ? new BookingTableDto { id = b.table!.id, name = b.table.name }
                    : null,
                customer = b.childId != null
                    ? new BookingCustomerDto
                    {
                        childId = b.child!.id,
                        name = b.child.name,
                        parentId = b.child.parent != null ? b.child.parent.id : (int?)null,
                        parentName = b.child.parent != null ? b.child.parent.name : null,
                        parentLastName = b.child.parent != null ? b.child.parent.lastName : null,
                        phone = b.child.parent != null ? b.child.parent.phone : null
                    }
                    : null,
                price = b.price,
                startTime = b.startTime,
                endTime = b.endTime,
                subscriptionStartTime = b.subscriptionStartTime,
                subscriptionEndTime = b.subscriptionEndTime,
                status = b.status.ToString(),
                note = b.note
            }).ToListAsync();

            return new PagedResult<BookingListItemDto>
            {
                items = items,
                page = page,
                size = size,
                totalSize = totalSize,
                totalPages = page == -1 ? 1 : (int)Math.Ceiling((double)totalSize / size)
            };
        }

        public async Task<Booking?> GetById(int id) =>
            await _context.Bookings
                .AsNoTracking()
                .Include(b => b.table)
                .Include(b => b.child)
                    .ThenInclude(c => c.parent)
                .FirstOrDefaultAsync(b => b.id == id);

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

            existingBooking.tableId = updatedBooking.tableId ?? existingBooking.tableId;
            existingBooking.startTime = updatedBooking.startTime ?? existingBooking.startTime;
            existingBooking.endTime = updatedBooking.endTime ?? existingBooking.endTime;
            existingBooking.status = updatedBooking.status;
            existingBooking.price = updatedBooking.price ?? existingBooking.price;
            existingBooking.childId = updatedBooking.childId ?? existingBooking.childId;
            existingBooking.note = updatedBooking.note ?? existingBooking.note;
            existingBooking.subscriptionStartTime = updatedBooking.subscriptionStartTime ?? existingBooking.subscriptionStartTime;
            existingBooking.subscriptionEndTime = updatedBooking.subscriptionEndTime ?? existingBooking.subscriptionEndTime;

            // Eğer bir paket (Purchase) kullanılıyorsa, onu da bağla (usp_update_booking logic)
            if (updatedBooking.purchaseId.HasValue)
            {
                var exists = await _context.PurchaseBookings.AnyAsync(pb => pb.bookingId == existingBooking.id && pb.purchaseId == updatedBooking.purchaseId.Value);
                if (!exists)
                {
                    _context.PurchaseBookings.Add(new PurchaseBooking
                    {
                        bookingId = existingBooking.id,
                        purchaseId = updatedBooking.purchaseId.Value
                    });
                }
            }

            await _context.SaveChangesAsync();
            return existingBooking;
        }
    }
}
