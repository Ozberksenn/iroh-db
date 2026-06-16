using Iroh.Models.DTOs.Booking;

namespace Iroh.Models.DTOs.Purchase
{
    // fn_get_purchase_by_customer_id satır şekli.
    public class CustomerPurchaseResultDto
    {
        public long Id { get; set; }
        public long CustomerId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public double Hours { get; set; }
        public double Price { get; set; }
        // Değer DAKİKA cinsinden; client bu alanı tarihsel olarak "usedHours" adıyla (dakika değeriyle) okuyor.
        public double UsedHours { get; set; }
        public List<PaymentDto> Payments { get; set; } = new();
    }

    // usp_get_purchase_bookings_by_id satır şekli.
    public class PurchaseBookingResultDto
    {
        public long Id { get; set; }
        public long booking_id { get; set; }
        public long purchase_id { get; set; }
        public BookingBriefDto? Booking { get; set; }
    }

    public class BookingBriefDto
    {
        public BookingTableDto? Table { get; set; }
        public int? TableId { get; set; }
        // D7: proc'taki b.customerId (var olmayan kolon → proc patlıyordu) yerine child.parentId'den türetildi.
        public int? CustomerId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Note { get; set; }
    }
}
