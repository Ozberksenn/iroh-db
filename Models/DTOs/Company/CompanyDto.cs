using Iroh.Models.Entities;

namespace Iroh.Models.DTOs.Company
{
    public class CompanyDto
    {
        public int id { get; set; }
        public required string name { get; set; }
        public int firstHourPrice { get; set; }
        public int additionalHalfHourPrice { get; set; }

        public static CompanyDto From(Models.Entities.Company c) => new()
        {
            id = c.id,
            name = c.name,
            firstHourPrice = c.firstHourPrice,
            additionalHalfHourPrice = c.additionalHalfHourPrice
        };
    }
}
