using System.ComponentModel.DataAnnotations;

namespace Iroh.Models.DTOs.Wallet
{
    // Abonelik / kredi ekleme. PackageId verilirse dakika/ücret/geçerlilik katalogdan (sunucu-otoriter) çözülür;
    // verilmezse Minutes (zorunlu) + Money (ops.) kullanılır.
    public class WalletCreditDto
    {
        [Required]
        public int CustomerId { get; set; }
        public int? PackageId { get; set; }
        public int? Minutes { get; set; }
        public decimal? Money { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
    }

    // Borç ödeme (serbest tutar, FIFO kapanır).
    public class WalletSettleDto
    {
        [Required]
        public int CustomerId { get; set; }
        [Required]
        public decimal Amount { get; set; }
    }

    // Denetlenebilir düzeltme: Minutes (zaman defteri) VEYA Amount (para defteri) — biri verilir.
    public class WalletAdjustDto
    {
        [Required]
        public int CustomerId { get; set; }
        public int? Minutes { get; set; }
        public decimal? Amount { get; set; }
        [Required]
        public string Reason { get; set; } = string.Empty;
    }

    // Oturum kapanışı: kapsanmayan süre için "PayNow" | "Debt".
    // Tamamlama tek uç (docs/wallet-redesign.md §4); booking alanları da burada finalize edilir.
    public class BookingCloseDto
    {
        public string Settlement { get; set; } = "PayNow";
        public DateTime? SubscriptionEndTime { get; set; }  // tam süre penceresi (BÖL'ü servis yapar); verilmezse now
        public DateTime? EndTime { get; set; }              // verilmezse now
        public string? Note { get; set; }
        public int? TableId { get; set; }
        public int? ChildId { get; set; }
    }
}
