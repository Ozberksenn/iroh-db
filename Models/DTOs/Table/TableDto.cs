namespace Iroh.Models.DTOs.Table
{
    // vw_tables: id, name (yalnızca isdeleted=false).
    public class TableDto
    {
        public int id { get; set; }
        public required string name { get; set; }
    }
}
