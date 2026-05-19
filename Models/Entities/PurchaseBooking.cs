using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    [Table("purchasebookings", Schema = "public")]
    public class PurchaseBooking : BaseEntity
    {
        [Column("bookingid")]
        public int bookingId { get; set; }

        [ForeignKey("bookingId")]
        public Booking? booking { get; set; }

        [Column("purchaseid")]
        public int purchaseId { get; set; }

        [ForeignKey("purchaseId")]
        public Purchase? purchase { get; set; }
    }
}