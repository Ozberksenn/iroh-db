using System.ComponentModel.DataAnnotations.Schema;
using Iroh.Models.Enums;

namespace Iroh.Models.Entities
{
    [Table("users", Schema = "public")]
    public class User : BaseEntity
    {
        [Column("name")]
        public required string Name { get; set; }

        [Column("lastname")]
        public string? LastName { get; set; }

        [Column("password")]
        public required string Password { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("mail")]
        public required string Mail { get; set; }

        [Column("isactive")]
        public required bool IsActive { get; set; } = true;

        // Yetki rolü. DB'de string ('User'/'Admin') saklanır (AppDbContext HasConversion<string>).
        // Yeni kullanıcılar varsayılan olarak 'User'; admin ataması ayrıca yapılır.
        [Column("role")]
        public UserRole Role { get; set; } = UserRole.User;
    }
}
