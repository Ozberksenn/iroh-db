
using System.ComponentModel.DataAnnotations;

namespace Iroh.Models.DTOs.Purchase
{
    public class PurchaseCreateDto
    {
        [Required]
        public required decimal hours { get; set; }

        [Required]
        public required decimal price { get; set; }

        public required int customerId { get; set; }

        public required DateTime startDate { get; set; }

        public required DateTime endDate { get; set; }

    }
}