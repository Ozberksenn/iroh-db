using System.ComponentModel.DataAnnotations.Schema;
using Iroh.Models.Enums;

namespace Iroh.Models.Entities
{
    // Para/borç hesabı defteri — append-only. amount_delta: Charge −, Payment +.
    [Table("cash_ledger", Schema = "public")]
    public class CashLedgerEntry : BaseEntity
    {
        [Column("wallet_id")]
        public int WalletId { get; set; }

        [Column("type")]
        public CashLedgerType Type { get; set; }

        [Column("amount_delta")]
        public decimal AmountDelta { get; set; }   // Charge − , Payment +

        [Column("booking_id")]
        public int? BookingId { get; set; }        // oturum ücreti ise

        [Column("reason")]
        public string? Reason { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
