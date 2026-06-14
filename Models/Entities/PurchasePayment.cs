using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    [Table("purchasepayments", Schema = "public")]
    public class PurchasePayment : BaseEntity
    {
        [Column("hours")]
        public decimal Hours { get; set; }

        [Column("price")]
        public decimal Price { get; set; }

        [Column("purchaseid")]
        public int PurchaseId { get; set; }

        [ForeignKey("PurchaseId")]
        public Purchase? Purchase { get; set; }
    }
}
