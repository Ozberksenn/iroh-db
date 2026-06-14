
using System.ComponentModel.DataAnnotations;

namespace Iroh.Models.DTOs.PurchasePayment
{
    public class PurchasePaymentCreateDto
    {
        [Required]
        public required decimal hours { get; set; }

        [Required]
        public required decimal price { get; set; }

        [Required]
        public required int purchaseId { get; set; }
    }
}