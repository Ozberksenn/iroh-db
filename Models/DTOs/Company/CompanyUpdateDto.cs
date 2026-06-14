using System.ComponentModel.DataAnnotations;

namespace Iroh.Models.DTOs.Company
{
    public class CompanyUpdateDto
    {
        [Required]
        public int id { get; set; }

        public required string name { get; set; }

        public required int firstHourPrice { get; set; }

        public required int additionalHalfHourPrice { get; set; }
    }
}