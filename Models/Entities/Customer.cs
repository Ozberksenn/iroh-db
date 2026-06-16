using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    [Table("customers", Schema = "public")]
    public class Customer : BaseEntity
    {
        [Column("name")]
        public required string Name { get; set; }

        [Column("lastname")]
        public string? LastName { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("mail")]
        public string? Mail { get; set; }

        [Column("createdat")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updatedat")]
        public DateTime? UpdatedAt { get; set; }

        [Column("isdeleted")]
        public bool IsDeleted { get; set; } = false;
    }
}
