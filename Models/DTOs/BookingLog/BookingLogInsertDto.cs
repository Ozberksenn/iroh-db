
using Iroh.Models.Enums;

namespace Iroh.Models.DTOs.BookingLog
{
    public class BookingLogInsertDto
    {
        public required int BookingId { get; set; }

        public required DateTime Time { get; set; }

        public required BookingLogType Type { get; set; }

        public required int UserId { get; set; }
    }
}