using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    [Table("packages", Schema = "public")]
    public class Package : BaseEntity
    {
        public required string name { get; set; }
        public required decimal hours { get; set; }
        public required decimal price { get; set; }

        [Column("validity_days")]
        public int? validityDays { get; set; }

        [Column("is_deleted")]
        public bool isDeleted { get; set; } = false;

        [Column("created_at")]
        public DateTime createdAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime updatedAt { get; set; } = DateTime.Now;
    }
}