
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.DTOs.Customer
{
    public class CustomerUpdateDto
    {
        [Required]
        public int id { get; set; }

        [Required]
        public required string name { get; set; }

        public string? lastName { get; set; }

        public string? phone { get; set; }

        public string? mail { get; set; }

    }
}