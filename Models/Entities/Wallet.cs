using System.ComponentModel.DataAnnotations.Schema;

namespace Iroh.Models.Entities
{
    // Müşteri (parent) başına TEK cüzdan. İki materyalize bakiye; defterler (time_ledger / cash_ledger) otorite.
    // Tasarım: docs/wallet-redesign.md
    [Table("wallets", Schema = "public")]
    public class Wallet : BaseEntity
    {
        [Column("customer_id")]
        public int CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        // Σ time_ledger.minutes_delta — canlı (açık) oturum HARİÇ finalize bakiye.
        [Column("time_balance_minutes")]
        public int TimeBalanceMinutes { get; set; }

        // Σ cash_ledger.amount_delta — NEGATİF ise müşteri borçludur (borç = max(0, -CashBalance)).
        [Column("cash_balance")]
        public decimal CashBalance { get; set; }

        // Σ time_ledger {DebtCharge,DebtSettle} — kapsanmayan (aşım) sürenin SÜRE cinsinden borcu (≥0).
        // Para-borcundan (cash_balance) ayrıdır; ödeme anında operatör para karşılığını girer.
        [Column("time_debt_minutes")]
        public int TimeDebtMinutes { get; set; }

        // Abonelik geçerlilik penceresi (tek pencere).
        [Column("valid_from")]
        public DateTime? ValidFrom { get; set; }

        [Column("valid_to")]
        public DateTime? ValidTo { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
