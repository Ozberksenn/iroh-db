using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    [Table("purchases", Schema = "public")]
    public class Purchase : BaseEntity
    {
        public required int hours { get; set; }
        public required int price { get; set; }

        [Column("createdat")]
        public required DateTime createdAt { get; set; }

        [Column("updatedat")]
        public DateTime? updatedAt { get; set; }

        [Column("customerid")]
        public int customerId { get; set; }

        [ForeignKey("customerId")]
        public Customer? customer { get; set; }

        [Column("startdate")]
        public DateTime? startDate { get; set; }

        [Column("enddate")]
        public DateTime? endDate { get; set; }
    }
}