
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.DTOs.Customer
{
    public class CustomerUpdateDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public required string Name { get; set; }

        public string? LastName { get; set; }

        public string? Phone { get; set; }

        public string? Mail { get; set; }

    }
}