using System.ComponentModel.DataAnnotations.Schema;
using Iroh.Models.Enums;

namespace Iroh.Models.Entities
{
    // Zaman hesabı defteri — append-only. Asla UPDATE/DELETE; düzeltme = yeni Correction satırı.
    [Table("time_ledger", Schema = "public")]
    public class TimeLedgerEntry : BaseEntity
    {
        [Column("wallet_id")]
        public int WalletId { get; set; }

        [Column("type")]
        public TimeLedgerType Type { get; set; }

        [Column("minutes_delta")]
        public int MinutesDelta { get; set; }   // + kredi, − tüketim

        [Column("package_id")]
        public int? PackageId { get; set; }      // Credit ise hangi katalogdan

        // Kova penceresi — yalnız Credit satırlarında dolu (docs/subscription-buckets.md).
        // Tüketim önce-süresi-dolacak kovadan düşülür; süresi dolan kovanın kalanı yanar.
        [Column("valid_from")]
        public DateTime? ValidFrom { get; set; }

        [Column("valid_to")]
        public DateTime? ValidTo { get; set; }

        [Column("booking_id")]
        public int? BookingId { get; set; }      // Consumption ise hangi oturum

        [Column("reason")]
        public string? Reason { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
