
using System.ComponentModel.DataAnnotations;

namespace Iroh.Models.DTOs.Customer
{
    public class CustomerCreateDto
    {
        [Required]
        public required string Name { get; set; }

        public string? LastName { get; set; }

        public string? Phone { get; set; }

        public string? Mail { get; set; }

    }
}