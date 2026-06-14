namespace Iroh.Models.DTOs.Booking
{
    // usp_get_bookings satır şekli.
    public class BookingListItemDto
    {
        public int id { get; set; }
        public BookingTableDto? table { get; set; }
        public BookingCustomerDto? customer { get; set; }
        public decimal? price { get; set; }
        public DateTime? startTime { get; set; }
        public DateTime? endTime { get; set; }
        public DateTime? subscriptionStartTime { get; set; }
        public DateTime? subscriptionEndTime { get; set; }
        public string status { get; set; } = string.Empty;
        public string? note { get; set; }
    }

    public class BookingTableDto
    {
        public int? id { get; set; }
        public string? name { get; set; }
    }

    public class BookingCustomerDto
    {
        public int? childId { get; set; }
        public string? name { get; set; }
        public int? parentId { get; set; }
        public string? parentName { get; set; }
        public string? parentLastName { get; set; }
        public string? phone { get; set; }
    }
}
