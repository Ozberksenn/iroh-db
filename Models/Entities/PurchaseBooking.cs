using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    [Table("purchasebookings", Schema = "public")]
    public class PurchaseBooking : BaseEntity
    {
        [Column("bookingid")]
        public int BookingId { get; set; }

        [ForeignKey("BookingId")]
        public Booking? Booking { get; set; }

        [Column("purchaseid")]
        public int PurchaseId { get; set; }

        [ForeignKey("PurchaseId")]
        public Purchase? Purchase { get; set; }
    }
}
