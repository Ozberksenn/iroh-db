using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    [Table("users", Schema = "public")]
    public class User : BaseEntity
    {

        public required string name { get; set; }

        [Column("lastname")]
        public string? lastname { get; set; }

        public required string password { get; set; }

        public string? phone { get; set; }

        public required string mail { get; set; }

        [Column("isactive")]
        public required bool isActive { get; set; } = true;

    }
}