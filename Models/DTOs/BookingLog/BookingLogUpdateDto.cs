using Iroh.Models.Enums;

namespace Iroh.Models.DTOs.BookingLog
{
    public class BookingLogUpdateDto
    {
        public int BookingId { get; set; }

        public DateTime Time { get; set; }

        public BookingLogType Type { get; set; }

        public int UserId { get; set; }
    }
}
