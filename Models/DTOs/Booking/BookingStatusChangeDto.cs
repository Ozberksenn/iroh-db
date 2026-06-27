namespace Iroh.Models.DTOs.Booking
{
    // pause / resume / cancel uçlarının gövdesi. userId sunucuda JWT'den okunur (client göndermez).
    public class BookingStatusChangeDto
    {
        public int? MinutesAgo { get; set; }   // geriye dönük zaman (0 = şimdi)
        public string? Note { get; set; }
    }
}
