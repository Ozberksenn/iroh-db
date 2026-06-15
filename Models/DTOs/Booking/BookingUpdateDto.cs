

using Iroh.Models.Enums;

namespace Iroh.Models.DTOs.Booking
{
    public class BookingUpdateDto
    {
        public int id { get; set; }
        public int? tableId { get; set; }
        public DateTime? startTime { get; set; }
        public DateTime? endTime { get; set; }
        public required BookingStatus status { get; set; }
        public int? price { get; set; }
        public int? childId { get; set; }
        public string? note { get; set; }
        public int? purchaseId { get; set; }
        public DateTime? subscriptionStartTime { get; set; }
        public DateTime? subscriptionEndTime { get; set; }
    }
}