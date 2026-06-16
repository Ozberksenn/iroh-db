using System.ComponentModel.DataAnnotations;

namespace Iroh.Models.DTOs.Company
{
    public class CompanyUpdateDto
    {
        [Required]
        public int Id { get; set; }

        public required string Name { get; set; }

        public required int FirstHourPrice { get; set; }

        public required int AdditionalHalfHourPrice { get; set; }
    }
}