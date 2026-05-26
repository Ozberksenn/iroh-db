using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Iroh.Models.Entities
{
    [Table("bookinglogs", Schema = "public")]
    public class BookingLog : BaseEntity
    {
        [Column("bookingid")]
        public int bookingId { get; set; }

        [ForeignKey("bookingId")]
        [JsonIgnore]
        public Booking? booking { get; set; }

        [Column("time")]
        public DateTime time { get; set; } = DateTime.UtcNow;

        [Column("type")]
        public required string type { get; set; }

        [Column("userid")]
        public int? userId { get; set; }
    }
}
