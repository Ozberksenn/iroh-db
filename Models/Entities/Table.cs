using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    // DB'deki gerçek kolonlar: id, name, isdeleted. (createdat/updatedat YOK.)
    [Table("tables", Schema = "public")]
    public class Table : BaseEntity
    {
        [Column("name")]
        public required string Name { get; set; }

        [Column("isdeleted")]
        public bool IsDeleted { get; set; } = false;
    }
}
