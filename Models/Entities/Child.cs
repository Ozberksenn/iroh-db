using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Iroh.Models.Entities
{
    [Table("children", Schema = "public")]
    public class Child : BaseEntity
    {
        public required string name { get; set; }

        [Column("parent_id")]
        public int parentId { get; set; }

        [ForeignKey("parentId")]
        [JsonIgnore]
        public Customer? parent { get; set; }

        [Column("birth_date")]
        public DateTime birthDate { get; set; }

        [Column("created_at")]
        public DateTime createdAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime updatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_deleted")]
        public bool isDeleted { get; set; } = false;
    }
}