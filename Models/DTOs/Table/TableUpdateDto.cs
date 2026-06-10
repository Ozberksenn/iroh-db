using System.ComponentModel.DataAnnotations;

namespace Iroh.Models.DTOs.Table
{
    public class TableUpdateDto
    {
        [Required]
        public int id { get; set; }

        [Required]
        public required string name { get; set; }
    }
}