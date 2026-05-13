using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    [Table("company", Schema = "public")]
    public class Company : BaseEntity
    {

        public required string name { get; set; }

        [Column("firsthourprice")]
        public required int firstHourPrice { get; set; }

        [Column("additionalhalfhourprice")]
        public required int additionalHalfHourPrice { get; set; }
    }
}