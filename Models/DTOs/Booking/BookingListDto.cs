namespace Iroh.Models.DTOs.Booking
{
    // usp_get_bookings satır şekli.
    public class BookingListItemDto
    {
        public int Id { get; set; }
        public BookingTableDto? Table { get; set; }
        public BookingCustomerDto? Customer { get; set; }
        public decimal? Price { get; set; }                 // tahsil edilen ₺ (PayNow/misafir); Debt'te 0
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime? SubscriptionStartTime { get; set; }
        public DateTime? SubscriptionEndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Note { get; set; }
        // Ledger'dan türetilen kapanış kırılımı (gerçek kaynak; zaman farkından tahmin DEĞİL).
        public int CoveredMinutes { get; set; }              // abonelikten düşülen (Σ Consumption)
        public int DebtedMinutes { get; set; }               // borca yazılan süre (Σ DebtCharge)
    }

    public class BookingTableDto
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
    }

    public class BookingCustomerDto
    {
        public int? ChildId { get; set; }
        public string? Name { get; set; }
        public int? ParentId { get; set; }
        public string? ParentName { get; set; }
        public string? ParentLastName { get; set; }
        public string? Phone { get; set; }
    }
}
