using Iroh.Models.DTOs.Wallet;
using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Iroh.Services;
using Iroh.Exceptions;
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
                StartTime = subStart, EndTime = subEnd
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
                // A2: kısmi kapsamada (20 dk bakiye, 50 dk oturum → 30 dk aşım) öneri yok; tutar elle gelir.
                var res = await NewService(c).CloseBooking(3, SettlementMode.PayNow, userId: 9, chargeAmount: 30m);
                Assert.Equal(30m, res.Charged);
                Assert.Equal(0m, res.WalletAfter!.CashBalance);  // Charge + Payment net 0
            }
        }

        [Fact]
        public async Task CloseBooking_PartialOverage_PayNow_WithoutAmount_Throws()
        {
            // A2: kısmi kapsamada (abonelikten karşılanan + aşım) öneri YOK → chargeAmount zorunlu.
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
                await SeedFamily(c, subStart: now, subEnd: now.AddMinutes(50));   // 50 dk
            using (var c = NewContext(db))
                await NewService(c).CreditTime(1, 20, 0m, null, null, now.AddDays(-1), now.AddDays(1)); // 20 dk

            using (var c = NewContext(db))
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    NewService(c).CloseBooking(3, SettlementMode.PayNow, userId: 9));
        }

        [Fact]
        public async Task CloseBooking_FullSession_PayNow_AutoPricesWithoutAmount()
        {
            // A2: kapsama=0 (süresi dolmuş → tüm oturum aşım) ise öneri sunulur (PriceForMinutes(dur)).
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
                await SeedFamily(c, subStart: now, subEnd: now.AddMinutes(40));   // 40 dk
            using (var c = NewContext(db))
                // Bakiye var ama pencere geçmişte → cover=0, tüm süre aşım.
                await NewService(c).CreditTime(1, 100, 0m, null, null, now.AddDays(-10), now.AddDays(-5));

            using (var c = NewContext(db))
            {
                var res = await NewService(c).CloseBooking(3, SettlementMode.PayNow, userId: 9);
                Assert.Equal(0, res.CoveredMinutes);
                Assert.Equal(40, res.UncoveredMinutes);
                Assert.Equal(40m, res.Charged);                  // FakePricing 1₺/dk → 40
                Assert.Equal(0m, res.WalletAfter!.CashBalance);  // Charge + Payment net 0
            }
        }

        [Fact]
        public async Task CloseBooking_NonSubscriber_CannotDebt()
        {
            // A1: hiç abonelik almamış (Credit yok) müşteri süre-borçlanamaz → Debt reddedilir.
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
                await SeedFamily(c, subStart: now, subEnd: now.AddMinutes(40));   // kredi YOK

            using (var c = NewContext(db))
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    NewService(c).CloseBooking(3, SettlementMode.Debt, userId: 9));
        }

        [Fact]
        public async Task CloseBooking_ExpiredSubscription_TimeDebtsFullDuration()
        {
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
                await SeedFamily(c, subStart: now, subEnd: now.AddMinutes(40));
            using (var c = NewContext(db))
                // Bakiye var ama kova penceresi geçmişte → geçerli değil; kalanı YANAR (Aşama B).
                await NewService(c).CreditTime(1, 100, 0m, null, null, now.AddDays(-10), now.AddDays(-5));

            using (var c = NewContext(db))
            {
                var res = await NewService(c).CloseBooking(3, SettlementMode.Debt, userId: 9);
                Assert.Equal(0, res.CoveredMinutes);          // geçerli değil → abonelikten düşmez
                Assert.Equal(40, res.UncoveredMinutes);
                Assert.Equal(40, res.DebtedMinutes);
                Assert.Equal(40, res.WalletAfter!.TimeDebtMinutes);     // tüm süre borca (SÜRE)
                Assert.Equal(0, res.WalletAfter.TimeBalanceMinutes);    // süresi dolmuş kova kullanılamaz (availableNow=0)
                Assert.Equal(100, res.WalletAfter.BurnedMinutes);       // 100 dk yandı
                Assert.Equal(0m, res.WalletAfter.CashBalance);          // nakit hareketi yok
            }
        }

        [Fact]
        public async Task CloseBooking_DrawsAcrossMultipleBuckets()
        {
            // Aşama B: top-up → iki kova; tek oturum ikisinden toplam kapsanır.
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
                await SeedFamily(c, subStart: now, subEnd: now.AddMinutes(90));   // 90 dk
            using (var c = NewContext(db))
            {
                await NewService(c).CreditTime(1, 60, 0m, null, null, now.AddDays(-1), now.AddDays(5));
                await NewService(c).CreditTime(1, 60, 0m, null, null, now.AddDays(-1), now.AddDays(10));
            }
            using (var c = NewContext(db))
            {
                var res = await NewService(c).CloseBooking(3, SettlementMode.PayNow, userId: 9);
                Assert.Equal(90, res.CoveredMinutes);                  // 60+60 → 90 kapsandı
                Assert.Equal(0, res.UncoveredMinutes);
                Assert.Equal(30, res.WalletAfter!.TimeBalanceMinutes); // 120 − 90
            }
        }

        [Fact]
        public void ComputeBuckets_EarliestExpiryFirst_ThenBurns()
        {
            // İki geçerli kova; tüketim önce-süresi-dolacaktan düşer. Erken kova dolunca kalanı yanar.
            var t0 = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
            var rows = new TimeLedgerEntry[]
            {
                new() { Id = 1, WalletId = 1, Type = TimeLedgerType.Credit, MinutesDelta = 60,
                        ValidFrom = t0.AddDays(-1), ValidTo = t0.AddDays(1),  CreatedAt = t0.AddDays(-1) }, // erken biter
                new() { Id = 2, WalletId = 1, Type = TimeLedgerType.Credit, MinutesDelta = 60,
                        ValidFrom = t0.AddDays(-1), ValidTo = t0.AddDays(10), CreatedAt = t0.AddDays(-1) }, // geç biter
                new() { Id = 3, WalletId = 1, Type = TimeLedgerType.Consumption, MinutesDelta = -50, CreatedAt = t0 },
            };

            // t0'da ikisi de geçerli: 50 önce erken kovadan (60→10), kalan 60 → toplam 70.
            var atT0 = WalletService.ComputeBuckets(rows, t0);
            Assert.Equal(70, atT0.AvailableNow);
            Assert.True(atT0.HasValidNow);

            // 2 gün sonra erken kova (ValidTo t0+1) doldu → kalan 10 yanar; yalnız geç kova (60) kalır.
            var later = WalletService.ComputeBuckets(rows, t0.AddDays(2));
            Assert.Equal(60, later.AvailableNow);
            Assert.Equal(10, later.Burned);
            Assert.True(later.HasValidNow);
        }

        [Fact]
        public void ComputeBuckets_Upcoming_And_CorrectionPool()
        {
            var t0 = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
            // İleri tarihli kova + süresiz manuel düzeltme havuzu.
            var rows = new TimeLedgerEntry[]
            {
                new() { Id = 1, WalletId = 1, Type = TimeLedgerType.Credit, MinutesDelta = 100,
                        ValidFrom = t0.AddDays(2), ValidTo = t0.AddDays(5), CreatedAt = t0 },              // upcoming
                new() { Id = 2, WalletId = 1, Type = TimeLedgerType.Correction, MinutesDelta = 30, CreatedAt = t0 }, // havuz
            };
            var bs = WalletService.ComputeBuckets(rows, t0);
            Assert.False(bs.HasValidNow);        // kova henüz başlamadı
            Assert.True(bs.HasUpcoming);
            Assert.True(bs.HasAny);
            Assert.Equal(30, bs.AvailableNow);   // yalnız havuz (Correction süresiz)
        }

        [Fact]
        public void Derive_ActiveRequiresBalance_DepletedIsSubscriber()
        {
            // Aktif = geçerli pencere VE bakiye>0. Geçerli ama bakiye 0 (tükenmiş) → Pasif (Subscriber).
            Assert.Equal(SubscriptionStatus.ActiveSubscriber, WalletService.Derive(validNow: true, hasBalance: true, hasUpcoming: false, hasAny: true));
            Assert.Equal(SubscriptionStatus.Subscriber, WalletService.Derive(validNow: true, hasBalance: false, hasUpcoming: false, hasAny: true));   // geçerli + 0 bakiye → pasif
            Assert.Equal(SubscriptionStatus.UpcomingSubscriber, WalletService.Derive(validNow: false, hasBalance: false, hasUpcoming: true, hasAny: true));
            Assert.Equal(SubscriptionStatus.Subscriber, WalletService.Derive(validNow: false, hasBalance: false, hasUpcoming: false, hasAny: true));  // süresi dolmuş → pasif
            Assert.Equal(SubscriptionStatus.Customer, WalletService.Derive(validNow: false, hasBalance: false, hasUpcoming: false, hasAny: false));
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
                c.Bookings.Add(new Booking { Id = 3, Status = BookingStatus.Active, StartTime = now, EndTime = now.AddMinutes(30) });
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
