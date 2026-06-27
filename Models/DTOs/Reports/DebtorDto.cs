namespace Iroh.Models.DTOs.Reports
{
    // Borçlu müşteri (alacak) satırı. Borç iki biçimde: para borcu (cash_balance < 0)
    // ve süre borcu (time_debt_minutes > 0). Sistem Misafiri'nin cüzdanı yoktur → listede çıkmaz.
    public class DebtorDto
    {
        public int CustomerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public decimal MoneyOwed { get; set; }       // = max(0, -cash_balance)
        public int TimeDebtMinutes { get; set; }
    }
}
