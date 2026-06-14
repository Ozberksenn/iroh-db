namespace Iroh.Models.DTOs.Customer
{
    // fn_get_customers satır şekli: id, name, lastName, phone, mail, status (status SQL'de hesaplanıyordu).
    public class CustomerListItemDto
    {
        public int id { get; set; }
        public required string name { get; set; }
        public string? lastName { get; set; }
        public string? phone { get; set; }
        public string? mail { get; set; }
        public string status { get; set; } = "Customer";
    }
}
