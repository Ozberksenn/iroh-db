
using System.ComponentModel.DataAnnotations;

namespace Iroh.Models.DTOs.Purchase
{
    public class PurchaseCreateDto
    {
        [Required]
        public required decimal Hours { get; set; }

        [Required]
        public required decimal Price { get; set; }

        public required int CustomerId { get; set; }

        public required DateTime StartDate { get; set; }

        public required DateTime EndDate { get; set; }

    }
}