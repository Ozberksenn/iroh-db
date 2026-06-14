using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Iroh.Models.Entities
{
    [Table("children", Schema = "public")]
    public class Child : BaseEntity
    {
        [Column("name")]
        public required string Name { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [ForeignKey("ParentId")]
        [JsonIgnore]
        public Customer? Parent { get; set; }

        [Column("birth_date")]
        public DateTime BirthDate { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;
    }
}
