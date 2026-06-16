using Iroh.Models.Entities;

namespace Iroh.Models.DTOs.Booking
{
    // Booking create/update yanıtı (navigation/entity sızdırmaz).
    public class BookingDto
    {
        public int Id { get; set; }
        public int? TableId { get; set; }
        public int? ChildId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime? SubscriptionStartTime { get; set; }
        public DateTime? SubscriptionEndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public string? Note { get; set; }

        public static BookingDto From(Models.Entities.Booking b) => new()
        {
            Id = b.Id,
            TableId = b.TableId,
            ChildId = b.ChildId,
            StartTime = b.StartTime,
            EndTime = b.EndTime,
            SubscriptionStartTime = b.SubscriptionStartTime,
            SubscriptionEndTime = b.SubscriptionEndTime,
            Status = b.Status.ToString(),
            Price = b.Price,
            Note = b.Note
        };
    }
}
