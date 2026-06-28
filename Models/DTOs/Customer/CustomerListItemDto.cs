namespace Iroh.Models.DTOs.Customer
{
    // fn_get_customers satır şekli: id, name, lastName, phone, mail, status (status SQL'de hesaplanıyordu).
    public class CustomerListItemDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public string? Mail { get; set; }
        public string Status { get; set; } = "Customer";
        public int TimeDebtMinutes { get; set; }   // süre-borcu (dk); 0 = borçsuz. Statüden bağımsız rozet için.
    }
}
