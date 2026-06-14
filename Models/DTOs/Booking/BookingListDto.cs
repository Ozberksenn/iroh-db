namespace Iroh.Models.DTOs.Booking
{
    // usp_get_bookings satır şekli.
    public class BookingListItemDto
    {
        public int Id { get; set; }
        public BookingTableDto? Table { get; set; }
        public BookingCustomerDto? Customer { get; set; }
        public decimal? Price { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime? SubscriptionStartTime { get; set; }
        public DateTime? SubscriptionEndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Note { get; set; }
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
