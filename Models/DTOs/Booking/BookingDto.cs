using Iroh.Models.Entities;

namespace Iroh.Models.DTOs.Booking
{
    // Booking create/update yanıtı (navigation/entity sızdırmaz).
    public class BookingDto
    {
        public int id { get; set; }
        public int? tableId { get; set; }
        public int? childId { get; set; }
        public DateTime? startTime { get; set; }
        public DateTime? endTime { get; set; }
        public DateTime? subscriptionStartTime { get; set; }
        public DateTime? subscriptionEndTime { get; set; }
        public string status { get; set; } = string.Empty;
        public decimal? price { get; set; }
        public string? note { get; set; }

        public static BookingDto From(Models.Entities.Booking b) => new()
        {
            id = b.id,
            tableId = b.tableId,
            childId = b.childId,
            startTime = b.startTime,
            endTime = b.endTime,
            subscriptionStartTime = b.subscriptionStartTime,
            subscriptionEndTime = b.subscriptionEndTime,
            status = b.status.ToString(),
            price = b.price,
            note = b.note
        };
    }
}
