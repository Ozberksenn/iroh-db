
using System.ComponentModel.DataAnnotations;

namespace Iroh.Models.DTOs.Purchase
{
    public class PurchaseUpdateDto
    {
        [Required]
        public int Id { get; set; }

        public required decimal Hours { get; set; }

        public required decimal Price { get; set; }

        public required int CustomerId { get; set; }

        public required DateTime StartDate { get; set; }

        public required DateTime EndDate { get; set; }

    }
}