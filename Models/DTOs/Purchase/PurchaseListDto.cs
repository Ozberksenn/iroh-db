namespace Iroh.Models.DTOs.Purchase
{
    // vw_purchases: id, hours, price, customerId, startDate, endDate (entity'nin createdAt/updatedAt/nav alanları sızdırılmaz).
    public class PurchaseListDto
    {
        public int Id { get; set; }
        public decimal Hours { get; set; }
        public decimal Price { get; set; }
        public int CustomerId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
