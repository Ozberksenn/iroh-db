using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    [Table("purchasepayments", Schema = "public")]
    public class PurchasePayment : BaseEntity
    {
        public int hours { get; set; }
        public int price { get; set; }

        [Column("purchaseid")]
        public int purchaseId { get; set; }

        [ForeignKey("purchaseId")]
        public Purchase? purchase { get; set; }
    }
}