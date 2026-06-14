using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    public class BaseEntity
    {
        [Column("id")]
        public int Id { get; set; }
    }
}
