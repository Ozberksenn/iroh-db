using System.ComponentModel.DataAnnotations;

namespace Iroh.Models.DTOs.Company
{
    public class CompanyUpdateDto
    {
        public required string name { get; set; }

        public required int firstHourPrice { get; set; }

        public required int additionalHalfHourPrice { get; set; }
    }
}