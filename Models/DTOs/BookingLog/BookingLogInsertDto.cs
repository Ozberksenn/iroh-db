
using Iroh.Models.Enums;

namespace Iroh.Models.DTOs.BookingLog
{
    public class BookingLogInsertDto
    {
        public required int bookingId { get; set; }

        public required DateTime time { get; set; }

        public required BookingLogType type { get; set; }

        public required int userId { get; set; }
    }
}