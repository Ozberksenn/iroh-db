using System.ComponentModel.DataAnnotations.Schema;
using Iroh.Models.Enums;

namespace Iroh.Models.Entities
{
    [Table("bookings", Schema = "public")]
    public class Booking : BaseEntity
    {
        [Column("tableid")]
        public int? TableId { get; set; }

        [ForeignKey("TableId")]
        public Table? Table { get; set; }

        [Column("starttime")]
        public DateTime? StartTime { get; set; }

        [Column("endtime")]
        public DateTime? EndTime { get; set; }

        [Column("status")]
        public BookingStatus Status { get; set; }

        [Column("price")]
        public decimal? Price { get; set; }

        [Column("note")]
        public string? Note { get; set; }

        [Column("subscriptionstarttime")]
        public DateTime? SubscriptionStartTime { get; set; }

        [Column("subscriptionendtime")]
        public DateTime? SubscriptionEndTime { get; set; }

        [Column("child_id")]
        public int? ChildId { get; set; }

        [ForeignKey("ChildId")]
        public Child? Child { get; set; }

        public ICollection<BookingLog> Logs { get; set; } = new List<BookingLog>();
    }
}
