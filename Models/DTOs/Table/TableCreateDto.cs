using System.ComponentModel.DataAnnotations;

namespace Iroh.Models.DTOs.Table
{
    public class TableCreateDto
    {
        [Required]
        public required string Name { get; set; }
    }
}