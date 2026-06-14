
using System.ComponentModel.DataAnnotations;

namespace Iroh.Models.DTOs.PurchasePayment
{
    public class PurchasePaymentCreateDto
    {
        [Required]
        public required decimal Hours { get; set; }

        [Required]
        public required decimal Price { get; set; }

        [Required]
        public required int PurchaseId { get; set; }
    }
}