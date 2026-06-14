using System.ComponentModel.DataAnnotations.Schema;
using Iroh.Models.Enums;

namespace Iroh.Models.Entities
{
    [Table("bookings", Schema = "public")]
    public class Booking : BaseEntity
    {
        [Column("tableid")]
        public int? tableId { get; set; }

        [ForeignKey("tableId")]
        public Table? table { get; set; }

        [Column("starttime")]
        public DateTime? startTime { get; set; }

        [Column("endtime")]
        public DateTime? endTime { get; set; }

        public BookingStatus status { get; set; }

        public decimal? price { get; set; }

        public string? note { get; set; }

        [Column("subscriptionstarttime")]
        public DateTime? subscriptionStartTime { get; set; }

        [Column("subscriptionendtime")]
        public DateTime? subscriptionEndTime { get; set; }

        [Column("child_id")]
        public int? childId { get; set; }

        [ForeignKey("childId")]
        public Child? child { get; set; }

        public ICollection<BookingLog> logs { get; set; } = new List<BookingLog>();
    }
}
