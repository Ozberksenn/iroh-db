namespace Iroh.Models.DTOs.Booking
{
    // vw_activebookings satır şekli (BookingTableDto BookingListDto.cs'te tanımlı, tekrar kullanılıyor).
    public class ActiveBookingDto
    {
        public int id { get; set; }
        public BookingTableDto? table { get; set; }
        public ActiveBookingCustomerDto? customer { get; set; }
        public decimal? price { get; set; }
        public DateTime? startTime { get; set; }
        public DateTime? endTime { get; set; }
        public DateTime? subscriptionStartTime { get; set; }
        public DateTime? subscriptionEndTime { get; set; }
        public string status { get; set; } = string.Empty;
        public string? note { get; set; }
        public List<BookingLogDto> logs { get; set; } = new();
    }

    public class ActiveBookingCustomerDto
    {
        public int? childId { get; set; }            // view'da customer.id = çocuk id
        public string? name { get; set; }            // çocuk adı
        public int? parentId { get; set; }
        public string? parentName { get; set; }
        public string? parentLastName { get; set; }
        public string? phone { get; set; }
        public string status { get; set; } = "Customer";  // abone kademesi
        public PurchaseInfoDto? purchase { get; set; }     // en iyi (best) paket
    }

    public class PurchaseInfoDto
    {
        public int id { get; set; }
        public decimal hours { get; set; }
        public decimal price { get; set; }
        public DateTime? startDate { get; set; }
        public DateTime? endDate { get; set; }
        public int customerId { get; set; }
        // D6: view bunu yanlışlıkla "usedHours" diye veriyordu; değer aslında DAKİKA. Doğru isimle.
        public double usedMinutes { get; set; }
        public List<PaymentDto> payments { get; set; } = new();
    }

    public class PaymentDto
    {
        public int id { get; set; }
        public int purchaseId { get; set; }
        public decimal hours { get; set; }
        public decimal price { get; set; }

        public static PaymentDto From(Iroh.Models.Entities.PurchasePayment p) => new()
        {
            id = p.id,
            purchaseId = p.purchaseId,
            hours = p.hours,
            price = p.price
        };
    }

    public class BookingLogDto
    {
        public int id { get; set; }
        public int bookingId { get; set; }
        public DateTime time { get; set; }
        public string type { get; set; } = string.Empty;
        public int? userId { get; set; }

        public static BookingLogDto From(Iroh.Models.Entities.BookingLog l) => new()
        {
            id = l.id,
            bookingId = l.bookingId,
            time = l.time,
            type = l.type.ToString(),
            userId = l.userId
        };
    }
}
