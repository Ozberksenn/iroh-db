using Iroh.Models.DTOs.Reports;
using Iroh.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    // F2 / B: okuma-amaçlı raporlar (Z-raporu + borçlular). Şema değişmeden defter/cüzdan agregasyonu.
    public interface IReportService
    {
        Task<ZReportDto> GetZReport(DateTime fromUtc, DateTime toUtc);
        Task<List<DebtorDto>> GetDebtors();
    }

    public class ReportService : IReportService
    {
        private readonly AppDbContext _context;

        public ReportService(AppDbContext context)
        {
            _context = context;
        }

        // F2.1: gün-sonu nakit özeti — cash_ledger'ı tarih aralığında tip/operatör bazında kır.
        public async Task<ZReportDto> GetZReport(DateTime fromUtc, DateTime toUtc)
        {
            var entries = await _context.CashLedger
                .Where(e => e.CreatedAt >= fromUtc && e.CreatedAt <= toUtc)
                .Select(e => new { e.Type, e.AmountDelta, e.UserId })
                .ToListAsync();

            var byType = entries
                .GroupBy(e => e.Type)
                .Select(g => new ZReportTypeLineDto
                {
                    Type = g.Key.ToString(),
                    Count = g.Count(),
                    Total = g.Sum(x => x.AmountDelta)
                })
                .OrderBy(x => x.Type)
                .ToList();

            var byUser = entries
                .GroupBy(e => e.UserId)
                .Select(g => new ZReportUserLineDto
                {
                    UserId = g.Key,
                    Count = g.Count(),
                    Total = g.Sum(x => x.AmountDelta)
                })
                .OrderBy(x => x.UserId)
                .ToList();

            decimal SumOf(CashLedgerType t) => entries.Where(e => e.Type == t).Sum(e => e.AmountDelta);

            var payment = SumOf(CashLedgerType.Payment);
            var refund = SumOf(CashLedgerType.Refund);
            var adjustment = SumOf(CashLedgerType.Adjustment);

            return new ZReportDto
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,
                TransactionCount = entries.Count,
                TotalPayment = payment,
                TotalCharge = SumOf(CashLedgerType.Charge),
                TotalRefund = refund,
                TotalAdjustment = adjustment,
                NetCash = payment + refund + adjustment,
                ByType = byType,
                ByUser = byUser
            };
        }

        // F2.2: borçlu müşteriler — para borcu (cash_balance < 0) veya süre borcu (time_debt_minutes > 0).
        // Misafir 999999'un cüzdanı olmadığından doğal olarak hariç.
        public async Task<List<DebtorDto>> GetDebtors()
        {
            var debtors = await _context.Wallets
                .Where(w => w.CashBalance < 0 || w.TimeDebtMinutes > 0)
                .Join(_context.Customers,
                    w => w.CustomerId,
                    c => c.Id,
                    (w, c) => new DebtorDto
                    {
                        CustomerId = c.Id,
                        Name = c.Name,
                        LastName = c.LastName,
                        Phone = c.Phone,
                        MoneyOwed = w.CashBalance < 0 ? -w.CashBalance : 0m,
                        TimeDebtMinutes = w.TimeDebtMinutes
                    })
                .ToListAsync();

            return debtors
                .OrderByDescending(d => d.MoneyOwed)
                .ThenByDescending(d => d.TimeDebtMinutes)
                .ToList();
        }
    }
}
