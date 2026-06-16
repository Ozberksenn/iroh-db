using Iroh.Models.Entities;

namespace Iroh.Models.DTOs.Company
{
    public class CompanyDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int FirstHourPrice { get; set; }
        public int AdditionalHalfHourPrice { get; set; }

        public static CompanyDto From(Models.Entities.Company c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            FirstHourPrice = c.FirstHourPrice,
            AdditionalHalfHourPrice = c.AdditionalHalfHourPrice
        };
    }
}
