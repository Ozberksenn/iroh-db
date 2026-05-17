using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    [Table("bookings", Schema = "public")]
    public class Booking : BaseEntity
    {
        [Column("tableid")]
        public int? tableId { get; set; }

        [Column("customerid")]
        public int? customerId { get; set; }

        [Column("starttime")]
        public DateTime? startTime { get; set; }

        [Column("endtime")]
        public DateTime? endTime { get; set; }

        public required string status { get; set; }

        public int? price { get; set; }

        public string? note { get; set; }

        [Column("subscriptionstarttime")]
        public DateTime? subscriptionStartTime { get; set; }

        [Column("subscriptionendtime")]
        public DateTime? subscriptionEndTime { get; set; }

        [Column("child_id")]
        public int? childId { get; set; }

    }
}