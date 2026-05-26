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
                .Include(b => b.customer)
                .ToList();
        }

        public Booking? GetById(int id)
        {
            return _context.Booking
                // .Include(b => b.table)
                // .Include(b => b.customer)
                .FirstOrDefault(b => b.id == id);
        }

        public List<Booking> GetActiveBookings()
        {
            // vw_activebookings mantığını C# tarafında LINQ ile karşılıyoruz.
            // Aktif veya Beklemede olan tüm oturumları, ilişkili verileriyle birlikte getiriyoruz.
            return _context.Booking
                .Include(b => b.table)
                .Include(b => b.customer)
                .Include(b => b.child)
                .Include(b => b.logs)
                .Where(b => b.status == Iroh.Models.Enums.BookingStatus.Active || b.status == Iroh.Models.Enums.BookingStatus.Paused)
                .ToList();
        }

        public Booking Create(Booking booking)
        {
            // fn_insert_booking fonksiyonunun mantığı: bookings tablosuna ekle ve oluşan id'yi dön.
            // EF Core zaten Add işleminden sonra 'booking.id' alanını otomatik doldurur.
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

            _context.SaveChanges();
            return existingBooking;
        }
    }
}
