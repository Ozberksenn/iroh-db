using Iroh.Models.DTOs.Booking;
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
                .Include(b => b.child)
                    .ThenInclude(c => c.parent)
                .ToList();
        }

        public Booking? GetById(int id)
        {
            return _context.Booking
                .Include(b => b.table)
                .Include(b => b.child)
                    .ThenInclude(c => c.parent)
                .FirstOrDefault(b => b.id == id);
        }

        public List<Booking> GetActiveBookings()
        {
            // vw_activebookings mantığını C# tarafında LINQ ile karşılıyoruz.
            return _context.Booking
                .Include(b => b.table)
                .Include(b => b.child)
                    .ThenInclude(c => c.parent)
                .Include(b => b.logs)
                .Where(b => b.status == Iroh.Models.Enums.BookingStatus.Active || b.status == Iroh.Models.Enums.BookingStatus.Paused)
                .ToList();
        }

        public Booking Create(Booking booking)
        {
            _context.Booking.Add(booking);
            _context.SaveChanges();
            return booking;
        }

        public Booking? Update(int id, BookingUpdateDto updatedBooking)
        {
            var existingBooking = _context.Booking.Find(id);
            if (existingBooking == null)
            {
                return null;
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
                var exists = _context.purchaseBookings.Any(pb => pb.bookingId == existingBooking.id && pb.purchaseId == updatedBooking.purchaseId.Value);
                if (!exists)
                {
                    _context.purchaseBookings.Add(new PurchaseBooking
                    {
                        bookingId = existingBooking.id,
                        purchaseId = updatedBooking.purchaseId.Value
                    });
                }
            }

            _context.SaveChanges();
            return existingBooking;
        }
    }
}
