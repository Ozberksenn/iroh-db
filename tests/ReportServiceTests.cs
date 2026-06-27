using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Iroh.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Iroh.Tests
{
    // F2 / B raporları: borçlular (alacak) + gün-sonu Z-raporu agregasyonu.
    public class ReportServiceTests
    {
        private static AppDbContext NewContext(string db) =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(db).Options);

        [Fact]
        public async Task GetDebtors_ReturnsOnlyDebtors_WithMoneyAndTimeDebt()
        {
            var db = Guid.NewGuid().ToString();
            using (var c = NewContext(db))
            {
                c.Customers.Add(new Customer { Id = 1, Name = "Ali", LastName = "V", Phone = "1" });
                c.Customers.Add(new Customer { Id = 2, Name = "Veli" });
                c.Customers.Add(new Customer { Id = 3, Name = "Borcsuz" });
                c.Wallets.Add(new Wallet { Id = 10, CustomerId = 1, CashBalance = -150m, TimeDebtMinutes = 0 });
                c.Wallets.Add(new Wallet { Id = 11, CustomerId = 2, CashBalance = 0m, TimeDebtMinutes = 45 });
                c.Wallets.Add(new Wallet { Id = 12, CustomerId = 3, CashBalance = 100m, TimeDebtMinutes = 0 });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var list = await new ReportService(c).GetDebtors();
                Assert.Equal(2, list.Count);

                var ali = list.Single(d => d.CustomerId == 1);
                Assert.Equal(150m, ali.MoneyOwed);
                Assert.Equal(0, ali.TimeDebtMinutes);

                var veli = list.Single(d => d.CustomerId == 2);
                Assert.Equal(0m, veli.MoneyOwed);
                Assert.Equal(45, veli.TimeDebtMinutes);

                Assert.DoesNotContain(list, d => d.CustomerId == 3);
            }
        }

        [Fact]
        public async Task GetZReport_AggregatesCashLedger_InRange_ByTypeAndUser()
        {
            var db = Guid.NewGuid().ToString();
            var from = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 6, 1, 23, 59, 59, DateTimeKind.Utc);
            var inRange = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var outRange = new DateTime(2026, 6, 2, 12, 0, 0, DateTimeKind.Utc);

            using (var c = NewContext(db))
            {
                c.CashLedger.Add(new CashLedgerEntry { WalletId = 1, Type = CashLedgerType.Payment, AmountDelta = 100m, UserId = 7, CreatedAt = inRange });
                c.CashLedger.Add(new CashLedgerEntry { WalletId = 1, Type = CashLedgerType.Charge, AmountDelta = -100m, UserId = 7, CreatedAt = inRange });
                c.CashLedger.Add(new CashLedgerEntry { WalletId = 1, Type = CashLedgerType.Refund, AmountDelta = -30m, UserId = 8, CreatedAt = inRange });
                c.CashLedger.Add(new CashLedgerEntry { WalletId = 1, Type = CashLedgerType.Payment, AmountDelta = 999m, UserId = 7, CreatedAt = outRange }); // aralık dışı
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var r = await new ReportService(c).GetZReport(from, to);
                Assert.Equal(3, r.TransactionCount);     // aralık dışı hariç
                Assert.Equal(100m, r.TotalPayment);
                Assert.Equal(-100m, r.TotalCharge);
                Assert.Equal(-30m, r.TotalRefund);
                Assert.Equal(0m, r.TotalAdjustment);
                Assert.Equal(70m, r.NetCash);            // 100 + (-30) + 0
                Assert.Equal(2, r.ByUser.Count);         // user 7 ve 8
                Assert.Equal(3, r.ByType.Count);         // Payment, Charge, Refund
            }
        }
    }
}
