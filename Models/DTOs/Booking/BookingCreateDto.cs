

using Iroh.Models.Enums;

namespace Iroh.Models.DTOs.Booking
{
    public class BookingCreateDto
    {
        public int? tableId { get; set; }
        public DateTime? startTime { get; set; }
        public DateTime? endTime { get; set; }
        public required BookingStatus status { get; set; }
        public decimal? price { get; set; }
        public int? childId { get; set; }
        public string? note { get; set; }
    }
}