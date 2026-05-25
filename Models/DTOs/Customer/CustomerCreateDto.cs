
using System.ComponentModel.DataAnnotations;

namespace Iroh.Models.DTOs.Customer
{
    public class CustomerCreateDto
    {
        [Required]
        public required string name { get; set; }

        public string? lastName { get; set; }

        public string? phone { get; set; }

        public string? mail { get; set; }

    }
}