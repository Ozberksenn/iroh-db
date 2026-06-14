using Iroh.Models.DTOs.Booking;

namespace Iroh.Models.DTOs.Purchase
{
    // fn_get_purchase_by_customer_id satır şekli.
    public class CustomerPurchaseResultDto
    {
        public long id { get; set; }
        public long customerId { get; set; }
        public DateTime? startDate { get; set; }
        public DateTime? endDate { get; set; }
        public double hours { get; set; }
        public double price { get; set; }
        // D6: proc bunu "usedHours" diye veriyordu ama değer DAKİKA. Doğru isimle.
        public double usedMinutes { get; set; }
        public List<PaymentDto> payments { get; set; } = new();
    }

    // usp_get_purchase_bookings_by_id satır şekli.
    public class PurchaseBookingResultDto
    {
        public long id { get; set; }
        public long booking_id { get; set; }
        public long purchase_id { get; set; }
        public BookingBriefDto? booking { get; set; }
    }

    public class BookingBriefDto
    {
        public BookingTableDto? table { get; set; }
        public int? tableId { get; set; }
        // D7: proc'taki b.customerId (var olmayan kolon → proc patlıyordu) yerine child.parentId'den türetildi.
        public int? customerId { get; set; }
        public DateTime? startTime { get; set; }
        public DateTime? endTime { get; set; }
        public string status { get; set; } = string.Empty;
        public string? note { get; set; }
    }
}
