using Iroh.Models.Entities;

namespace Iroh.Models.DTOs.Customer
{
    // Create/Update/Delete yanıtı — entity (isDeleted/createdAt/updatedAt) sızdırmaz.
    public class CustomerDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public string? Mail { get; set; }

        public static CustomerDto From(Models.Entities.Customer c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            LastName = c.LastName,
            Phone = c.Phone,
            Mail = c.Mail
        };
    }
}
