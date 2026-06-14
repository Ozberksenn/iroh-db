using Iroh.Models.Entities;

namespace Iroh.Models.DTOs.Customer
{
    // Create/Update/Delete yanıtı — entity (isDeleted/createdAt/updatedAt) sızdırmaz.
    public class CustomerDto
    {
        public int id { get; set; }
        public required string name { get; set; }
        public string? lastName { get; set; }
        public string? phone { get; set; }
        public string? mail { get; set; }

        public static CustomerDto From(Models.Entities.Customer c) => new()
        {
            id = c.id,
            name = c.name,
            lastName = c.lastName,
            phone = c.phone,
            mail = c.mail
        };
    }
}
