
using System.ComponentModel.DataAnnotations;

namespace Iroh.Models.DTOs.PurchasePayment
{
    public class PurchasePaymentCreateDto
    {
        [Required]
        public required int hours { get; set; }

        [Required]
        public required int price { get; set; }

        [Required]
        public required int purchaseId { get; set; }
    }
}