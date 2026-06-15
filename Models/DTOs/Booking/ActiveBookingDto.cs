namespace Iroh.Models.DTOs.Booking
{
    // vw_activebookings satır şekli (BookingTableDto BookingListDto.cs'te tanımlı, tekrar kullanılıyor).
    public class ActiveBookingDto
    {
        public int Id { get; set; }
        // Client aktif seans satırında bu alanları top-level okuyor (form pre-fill: complete/update/pause/continue/cancel).
        public int? TableId { get; set; }
        public int? ChildId { get; set; }
        public int? CustomerId { get; set; }   // client top-level customerId = ebeveyn id
        public BookingTableDto? Table { get; set; }
        public ActiveBookingCustomerDto? Customer { get; set; }
        public decimal? Price { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime? SubscriptionStartTime { get; set; }
        public DateTime? SubscriptionEndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Note { get; set; }
        public List<BookingLogDto> Logs { get; set; } = new();
    }

    public class ActiveBookingCustomerDto
    {
        public int? Id { get; set; }                 // client customer.id = çocuk id (999999 sistem-kullanıcı tespiti)
        public int? ChildId { get; set; }            // view'da customer.id = çocuk id
        public string? Name { get; set; }            // çocuk adı
        public int? ParentId { get; set; }
        public string? ParentName { get; set; }
        public string? ParentLastName { get; set; }
        public string? Phone { get; set; }
        public string Status { get; set; } = "Customer";  // abone kademesi
        public PurchaseInfoDto? Purchase { get; set; }     // en iyi (best) paket
    }

    public class PurchaseInfoDto
    {
        public int Id { get; set; }
        public decimal Hours { get; set; }
        public decimal Price { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int CustomerId { get; set; }
        // Değer DAKİKA cinsinden; client bu alanı tarihsel olarak "usedHours" adıyla (dakika değeriyle) okuyor.
        public double UsedHours { get; set; }
        public List<PaymentDto> Payments { get; set; } = new();
    }

    public class PaymentDto
    {
        public int Id { get; set; }
        public int PurchaseId { get; set; }
        public decimal Hours { get; set; }
        public decimal Price { get; set; }

        public static PaymentDto From(Iroh.Models.Entities.PurchasePayment p) => new()
        {
            Id = p.Id,
            PurchaseId = p.PurchaseId,
            Hours = p.Hours,
            Price = p.Price
        };
    }

    public class BookingLogDto
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public DateTime Time { get; set; }
        public string Type { get; set; } = string.Empty;
        public int? UserId { get; set; }

        public static BookingLogDto From(Iroh.Models.Entities.BookingLog l) => new()
        {
            Id = l.Id,
            BookingId = l.BookingId,
            Time = l.Time,
            Type = l.Type.ToString(),
            UserId = l.UserId
        };
    }
}
