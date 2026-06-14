namespace Iroh.Models.DTOs.Purchase
{
    // vw_purchases: id, hours, price, customerId, startDate, endDate (entity'nin createdAt/updatedAt/nav alanları sızdırılmaz).
    public class PurchaseListDto
    {
        public int id { get; set; }
        public decimal hours { get; set; }
        public decimal price { get; set; }
        public int customerId { get; set; }
        public DateTime? startDate { get; set; }
        public DateTime? endDate { get; set; }
    }
}
