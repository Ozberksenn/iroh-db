using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    [Table("purchases", Schema = "public")]
    public class Purchase : BaseEntity
    {
        [Column("hours")]
        public required decimal Hours { get; set; }

        [Column("price")]
        public required decimal Price { get; set; }

        [Column("createdat")]
        public required DateTime CreatedAt { get; set; }

        [Column("updatedat")]
        public DateTime? UpdatedAt { get; set; }

        [Column("customerid")]
        public int CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        [Column("startdate")]
        public DateTime? StartDate { get; set; }

        [Column("enddate")]
        public DateTime? EndDate { get; set; }
    }
}
