
using System.ComponentModel.DataAnnotations;

namespace Iroh.Models.DTOs.Purchase
{
    public class PurchaseCreateDto
    {
        [Required]
        public required int hours { get; set; }

        [Required]
        public required int price { get; set; }

        public required int customerId { get; set; }

        public required DateTime startDate { get; set; }

        public required DateTime endDate { get; set; }

    }
}