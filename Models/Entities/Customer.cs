using System.ComponentModel.DataAnnotations.Schema;
using Iroh.Models.Entities;

namespace Iroh.Models.Entities
{
    [Table("customers", Schema = "public")]
    public class Customer : BaseEntity
    {
        public required string name { get; set; }

        [Column("lastname")]
        public string? lastName { get; set; }

        public string? phone { get; set; }

        public string? mail { get; set; }

        [Column("createdat")]
        public DateTime createdAt { get; set; } = DateTime.UtcNow;

        [Column("updatedat")]
        public DateTime? updatedAt { get; set; }

        [Column("isdeleted")]
        public bool isDeleted { get; set; } = false;
    }
}