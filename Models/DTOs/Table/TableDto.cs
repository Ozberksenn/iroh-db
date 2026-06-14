namespace Iroh.Models.DTOs.Table
{
    // vw_tables: id, name (yalnızca isdeleted=false).
    public class TableDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }
}
