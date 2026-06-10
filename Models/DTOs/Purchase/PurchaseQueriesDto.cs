using System.Text.Json.Serialization;

namespace Iroh.Models.DTOs.Purchase
{
    public class CustomerPurchaseResultDto
    {
        public long id { get; set; }
        public long customerId { get; set; }
        public DateTime? startDate { get; set; }
        public DateTime? endDate { get; set; }
        public double hours { get; set; }
        public double price { get; set; }
        public double usedHours { get; set; }
        public string payments { get; set; } = "[]"; // JSON string from PG
    }

    public class PurchaseBookingResultDto
    {
        public long id { get; set; }
        public long booking_id { get; set; }
        public long purchase_id { get; set; }
        public string booking { get; set; } = "{}"; // JSON string from PG
    }
}
