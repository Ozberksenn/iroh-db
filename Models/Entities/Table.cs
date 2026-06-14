using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    // DB'deki gerçek kolonlar: id, name, isdeleted. (createdat/updatedat YOK.)
    [Table("tables", Schema = "public")]
    public class Table : BaseEntity
    {
        public required string name { get; set; }

        [Column("isdeleted")]
        public bool isDeleted { get; set; } = false;
    }
}
