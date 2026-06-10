using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    [Table("tables", Schema = "public")]
    public class Table : BaseEntity
    {

        public required string name { get; set; }

        [Column("isdeleted")]
        public bool isDeleted { get; set; } = false;

        [Column("createdat")]
        public DateTime createdAt { get; set; } = DateTime.Now;

        [Column("updatedat")]
        public DateTime? updatedAt { get; set; } = DateTime.Now;
    }
}