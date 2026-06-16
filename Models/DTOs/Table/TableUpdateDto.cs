using System.ComponentModel.DataAnnotations;

namespace Iroh.Models.DTOs.Table
{
    public class TableUpdateDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public required string Name { get; set; }
    }
}