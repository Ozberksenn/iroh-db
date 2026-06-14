using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    [Table("company", Schema = "public")]
    public class Company : BaseEntity
    {
        [Column("name")]
        public required string Name { get; set; }

        [Column("firsthourprice")]
        public required int FirstHourPrice { get; set; }

        [Column("additionalhalfhourprice")]
        public required int AdditionalHalfHourPrice { get; set; }
    }
}
