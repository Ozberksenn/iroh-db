

using Iroh.Models.Enums;

namespace Iroh.Models.DTOs.Booking
{
    public class BookingUpdateDto
    {
        public int Id { get; set; }
        public int? TableId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public required BookingStatus Status { get; set; }
        public decimal? Price { get; set; }
        public int? ChildId { get; set; }
        public string? Note { get; set; }
        public int? PurchaseId { get; set; }
    }
}