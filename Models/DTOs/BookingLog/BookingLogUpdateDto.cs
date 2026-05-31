using Iroh.Models.Enums;

namespace Iroh.Models.DTOs.BookingLog
{
    public class BookingLogUpdateDto
    {
        public int bookingId { get; set; }

        public DateTime time { get; set; }

        public BookingLogType type { get; set; }

        public int userId { get; set; }
    }
}
