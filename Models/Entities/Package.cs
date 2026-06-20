using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    [Table("packages", Schema = "public")]
    public class Package : BaseEntity
    {
        [Column("name")]
        public required string Name { get; set; }

        [Column("hours")]
        public required decimal Hours { get; set; }

        [Column("price")]
        public required decimal Price { get; set; }

        [Column("validity_days")]
        public int? ValidityDays { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
