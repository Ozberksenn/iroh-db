using Iroh.Models.Entities;
using Iroh.Models.Enums;

namespace Iroh.Models.DTOs.Wallet
{
    // Müşteri cüzdanının tam görünümü: iki bakiye + statü + borç + son hareketler.
    public class WalletDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }

        public int TimeBalanceMinutes { get; set; }     // finalize abonelik bakiyesi (canlı oturum hariç)
        public decimal CashBalance { get; set; }         // negatif = borç
        public decimal Debt { get; set; }                // max(0, -CashBalance) — kolaylık alanı

        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }

        public string Status { get; set; } = nameof(SubscriptionStatus.Customer);

        public List<TimeLedgerEntryDto> TimeLedger { get; set; } = new();
        public List<CashLedgerEntryDto> CashLedger { get; set; } = new();
    }

    // Oturum kapanış sonucu: kapsama/ücret özeti + güncel cüzdan.
    public class CloseBookingResultDto
    {
        public int BookingId { get; set; }
        public int DurationMinutes { get; set; }
        public bool HasWallet { get; set; }              // misafir/parent yoksa false
        public bool AlreadyProcessed { get; set; }       // idempotent tekrar
        public int CoveredMinutes { get; set; }          // abonelikten düşen
        public int UncoveredMinutes { get; set; }        // ücrete/borca giden
        public decimal Charged { get; set; }             // kapsanmayan süre ücreti
        public string? Settlement { get; set; }          // PayNow | Debt (ücret varsa)
        public WalletDto? WalletAfter { get; set; }
    }

    public class TimeLedgerEntryDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public int MinutesDelta { get; set; }
        public int? PackageId { get; set; }
        public int? BookingId { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }

        public static TimeLedgerEntryDto From(TimeLedgerEntry e) => new()
        {
            Id = e.Id,
            Type = e.Type.ToString(),
            MinutesDelta = e.MinutesDelta,
            PackageId = e.PackageId,
            BookingId = e.BookingId,
            Reason = e.Reason,
            CreatedAt = e.CreatedAt
        };
    }

    public class CashLedgerEntryDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal AmountDelta { get; set; }
        public int? BookingId { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }

        public static CashLedgerEntryDto From(CashLedgerEntry e) => new()
        {
            Id = e.Id,
            Type = e.Type.ToString(),
            AmountDelta = e.AmountDelta,
            BookingId = e.BookingId,
            Reason = e.Reason,
            CreatedAt = e.CreatedAt
        };
    }
}
