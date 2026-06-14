using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

using Iroh.Models.Enums;

namespace Iroh.Models.Entities
{
    [Table("bookinglogs", Schema = "public")]
    public class BookingLog : BaseEntity
    {
        [Column("bookingid")]
        public int BookingId { get; set; }

        [ForeignKey("BookingId")]
        [JsonIgnore]
        public Booking? Booking { get; set; }

        [Column("time")]
        public DateTime Time { get; set; } = DateTime.UtcNow;

        [Column("type")]
        public BookingLogType Type { get; set; }

        [Column("userid")]
        public int? UserId { get; set; }
    }
}
