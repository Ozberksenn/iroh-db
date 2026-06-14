using System.ComponentModel.DataAnnotations;
using Iroh.Models.Entities;

namespace Iroh.Models.DTOs.Package
{
    // Create/Update girişi — entity yerine (over-posting önlenir).
    public class PackageCreateDto
    {
        [Required] public required string name { get; set; }
        [Required] public decimal hours { get; set; }
        [Required] public decimal price { get; set; }
        public int? validityDays { get; set; }
    }

    public class PackageUpdateDto
    {
        [Required] public int id { get; set; }
        [Required] public required string name { get; set; }
        [Required] public decimal hours { get; set; }
        [Required] public decimal price { get; set; }
        public int? validityDays { get; set; }
    }

    // Yanıt — isDeleted/timestamps sızdırmaz.
    public class PackageDto
    {
        public int id { get; set; }
        public required string name { get; set; }
        public decimal hours { get; set; }
        public decimal price { get; set; }
        public int? validityDays { get; set; }

        public static PackageDto From(Models.Entities.Package p) => new()
        {
            id = p.id,
            name = p.name,
            hours = p.hours,
            price = p.price,
            validityDays = p.validityDays
        };
    }
}
