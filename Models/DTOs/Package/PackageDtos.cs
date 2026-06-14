using System.ComponentModel.DataAnnotations;
using Iroh.Models.Entities;

namespace Iroh.Models.DTOs.Package
{
    // Create/Update girişi — entity yerine (over-posting önlenir).
    public class PackageCreateDto
    {
        [Required] public required string Name { get; set; }
        [Required] public decimal Hours { get; set; }
        [Required] public decimal Price { get; set; }
        public int? ValidityDays { get; set; }
    }

    public class PackageUpdateDto
    {
        [Required] public int Id { get; set; }
        [Required] public required string Name { get; set; }
        [Required] public decimal Hours { get; set; }
        [Required] public decimal Price { get; set; }
        public int? ValidityDays { get; set; }
    }

    // Yanıt — isDeleted/timestamps sızdırmaz.
    public class PackageDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public decimal Hours { get; set; }
        public decimal Price { get; set; }
        public int? ValidityDays { get; set; }

        public static PackageDto From(Models.Entities.Package p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            Hours = p.Hours,
            Price = p.Price,
            ValidityDays = p.ValidityDays
        };
    }
}
