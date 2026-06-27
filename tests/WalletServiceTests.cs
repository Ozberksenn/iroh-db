using Iroh.Models.DTOs.Wallet;
using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Iroh.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Iroh.Tests
{
    // Cüzdan + iki defter çekirdeği (docs/wallet-redesign.md).
    // EF InMemory; idempotency explicit kontrolle sağlanır (InMemory partial index'i yok sayar).
    public class WalletServiceTests
    {
        private static AppDbContext NewContext(string db) =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(db).Options);

        // Deterministik fiyat: 1₺/dk → ücret == kapsanmayan dakika.
        private sealed class FakePricing : IPricingService
        {
            public Task<decimal> PriceForMinutes(int minutes) => Task.FromResult((decimal)minutes);
        }

        private static WalletService NewService(AppDbContext c) => new(c, new FakePricing());

        private static async Task SeedFamily(AppDbContext c, int customerId = 1, int childId = 2, int bookingId = 3,
                                             DateTime? subStart = null, DateTime? subEnd = null)
        {
            c.Customers.Add(new Customer { Id = customerId, Name = "Parent" });
            c.Children.Add(new Child { Id = childId, ParentId = customerId, Name = "Kid", IsDeleted = false });
            c.Bookings.Add(new Booking
            {
                Id = bookingId, ChildId = childId, Status = BookingStatus.Active,
                StartTime = subStart, SubscriptionStartTime = subStart, SubscriptionEndTime = subEnd
            });
            await c.SaveChangesAsync();
        }

        [Fact]
        public async Task CreditTime_AddsMinutes_AndDerivesActiveSubscriber()
        {
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
            {
                c.Customers.Add(new Customer { Id = 1, Name = "Parent" });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var wallet = await NewService(c).CreditTime(1, minutes: 120, money: 100m, packageId: null, userId: 9,
                    validFrom: now.AddDays(-1), validTo: now.AddDays(1));

                Assert.Equal(120, wallet.TimeBalanceMinutes);
                Assert.Equal(0m, wallet.CashBalance);          // peşin satış net 0
                Assert.Equal(nameof(SubscriptionStatus.ActiveSubscriber), wallet.Status);
            }
        }

        [Fact]
        public async Task CloseBooking_FullyCovered_DebitsTime_NoCharge()
        {
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
                await SeedFamily(c, subStart: now, subEnd: now.AddMinutes(30));   // 30 dk
            using (var c = NewContext(db))
                await NewService(c).CreditTime(1, 180, 0m, null, null, now.AddDays(-1), now.AddDays(1));

            using (var c = NewContext(db))
            {
                var res = await NewService(c).CloseBooking(3, SettlementMode.PayNow, userId: 9);
                Assert.True(res.HasWallet);
                Assert.Equal(30, res.CoveredMinutes);
                Assert.Equal(0, res.UncoveredMinutes);
                Assert.Equal(0m, res.Charged);
                Assert.Equal(150, res.WalletAfter!.TimeBalanceMinutes);   // 180 - 30
                Assert.Equal(0m, res.WalletAfter.CashBalance);
            }
        }

        [Fact]
        public async Task CloseBooking_PartialCoverage_Splits_AndTimeDebtsRemainder()
        {
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
                await SeedFamily(c, subStart: now, subEnd: now.AddMinutes(50));   // 50 dk
            using (var c = NewContext(db))
                await NewService(c).CreditTime(1, 20, 0m, null, null, now.AddDays(-1), now.AddDays(1)); // 20 dk

            using (var c = NewContext(db))
            {
                var res = await NewService(c).CloseBooking(3, SettlementMode.Debt, userId: 9);
                Assert.Equal(20, res.CoveredMinutes);
                Assert.Equal(30, res.UncoveredMinutes);
                Assert.Equal(30, res.DebtedMinutes);                     // aşım SÜRE borcuna yazıldı
                Assert.Equal(0m, res.Charged);                           // Debt'te para kesilmez
                Assert.Equal(0, res.WalletAfter!.TimeBalanceMinutes);    // 20 tükendi
                Assert.Equal(30, res.WalletAfter.TimeDebtMinutes);       // 30 dk süre borcu
                Assert.Equal(0m, res.WalletAfter.CashBalance);           // nakit hareketi yok
            }
        }

        [Fact]
        public async Task CloseBooking_PayNow_LeavesNoDebt()
        {
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
                await SeedFamily(c, subStart: now, subEnd: now.AddMinutes(50));
            using (var c = NewContext(db))
                await NewService(c).CreditTime(1, 20, 0m, null, null, now.AddDays(-1), now.AddDays(1));

            using (var c = NewContext(db))
            {
                var res = await NewService(c).CloseBooking(3, SettlementMode.PayNow, userId: 9);
                Assert.Equal(30m, res.Charged);
                Assert.Equal(0m, res.WalletAfter!.CashBalance);  // Charge + Payment net 0
            }
        }

        [Fact]
        public async Task CloseBooking_ExpiredSubscription_TimeDebtsFullDuration()
        {
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
                await SeedFamily(c, subStart: now, subEnd: now.AddMinutes(40));
            using (var c = NewContext(db))
                // Bakiye var ama pencere geçmişte → geçerli değil.
                await NewService(c).CreditTime(1, 100, 0m, null, null, now.AddDays(-10), now.AddDays(-5));

            using (var c = NewContext(db))
            {
                var res = await NewService(c).CloseBooking(3, SettlementMode.Debt, userId: 9);
                Assert.Equal(0, res.CoveredMinutes);          // geçerli değil → abonelikten düşmez
                Assert.Equal(40, res.UncoveredMinutes);
                Assert.Equal(40, res.DebtedMinutes);
                Assert.Equal(40, res.WalletAfter!.TimeDebtMinutes);     // tüm süre borca (SÜRE)
                Assert.Equal(100, res.WalletAfter.TimeBalanceMinutes);  // ölü bakiye dokunulmadı
                Assert.Equal(0m, res.WalletAfter.CashBalance);          // nakit hareketi yok
            }
        }

        [Fact]
        public async Task CloseBooking_IsIdempotent()
        {
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
                await SeedFamily(c, subStart: now, subEnd: now.AddMinutes(30));
            using (var c = NewContext(db))
                await NewService(c).CreditTime(1, 180, 0m, null, null, now.AddDays(-1), now.AddDays(1));

            using (var c = NewContext(db))
                await NewService(c).CloseBooking(3, SettlementMode.PayNow, 9);
            using (var c = NewContext(db))
            {
                var res = await NewService(c).CloseBooking(3, SettlementMode.PayNow, 9);
                Assert.True(res.AlreadyProcessed);
                Assert.Equal(150, res.WalletAfter!.TimeBalanceMinutes);  // ikinci kez düşmedi
            }
        }

        [Fact]
        public async Task CloseBooking_PayNow_UsesOperatorChargeAmount()
        {
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
                await SeedFamily(c, subStart: now, subEnd: now.AddMinutes(50));   // 50 dk
            using (var c = NewContext(db))
                await NewService(c).CreditTime(1, 20, 0m, null, null, now.AddDays(-1), now.AddDays(1)); // 20 dk

            using (var c = NewContext(db))
            {
                // 30 dk aşım; operatör Company oranı yerine 99₺ giriyor.
                var res = await NewService(c).CloseBooking(3, SettlementMode.PayNow, userId: 9, chargeAmount: 99m);
                Assert.Equal(30, res.UncoveredMinutes);
                Assert.Equal(99m, res.Charged);                          // operatör tutarı
                Assert.Equal(0m, res.WalletAfter!.CashBalance);          // peşin → net 0
                Assert.Equal(0, res.WalletAfter.TimeDebtMinutes);        // peşin → süre borcu yok
            }
        }

        [Fact]
        public async Task CreditTime_NetsExistingTimeDebt()
        {
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
                await SeedFamily(c, subStart: now, subEnd: now.AddMinutes(50));   // 50 dk
            using (var c = NewContext(db))
                await NewService(c).CreditTime(1, 20, 0m, null, null, now.AddDays(-1), now.AddDays(1)); // 20 dk
            using (var c = NewContext(db))
                await NewService(c).CloseBooking(3, SettlementMode.Debt, userId: 9);   // 30 dk süre borcu

            using (var c = NewContext(db))
            {
                // 50 dk yeni kredi: 30'u borcu kapatır, 20 kullanılabilir kalır.
                var w = await NewService(c).CreditTime(1, 50, 0m, null, 9, now.AddDays(-1), now.AddDays(1));
                Assert.Equal(0, w.TimeDebtMinutes);
                Assert.Equal(20, w.TimeBalanceMinutes);
            }
        }

        [Fact]
        public async Task SettleTimeDebt_ClearsDebt_AndRecordsCashNetZero()
        {
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
                await SeedFamily(c, subStart: now, subEnd: now.AddMinutes(50));
            using (var c = NewContext(db))
                await NewService(c).CreditTime(1, 20, 0m, null, null, now.AddDays(-1), now.AddDays(1));
            using (var c = NewContext(db))
                await NewService(c).CloseBooking(3, SettlementMode.Debt, userId: 9);   // 30 dk süre borcu

            using (var c = NewContext(db))
            {
                var w = await NewService(c).SettleTimeDebt(1, amount: 45m, userId: 9);
                Assert.Equal(0, w.TimeDebtMinutes);      // süre borcu kapandı
                Assert.Equal(0m, w.CashBalance);         // Charge + Payment net 0
            }
        }

        [Fact]
        public async Task CloseBooking_GuestOrNoParent_HasNoWallet()
        {
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
            {
                // Çocuğu olmayan oturum → parent yok.
                c.Bookings.Add(new Booking { Id = 3, Status = BookingStatus.Active, StartTime = now, SubscriptionEndTime = now.AddMinutes(30) });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var res = await NewService(c).CloseBooking(3, SettlementMode.PayNow, 9);
                Assert.False(res.HasWallet);
                Assert.Equal(BookingStatus.Completed, (await c.Bookings.FindAsync(3))!.Status);
            }
        }
    }
}
