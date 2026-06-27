using Iroh.Models.Entities;

namespace Iroh.Models.DTOs.Company
{
    public class CompanyDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public decimal FirstHourPrice { get; set; }
        public decimal AdditionalHalfHourPrice { get; set; }

        public static CompanyDto From(Models.Entities.Company c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            FirstHourPrice = c.FirstHourPrice,
            AdditionalHalfHourPrice = c.AdditionalHalfHourPrice
        };
    }
}
