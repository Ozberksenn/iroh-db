using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Iroh.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Iroh.Tests
{
    // Billing okuma çekirdeği (SubscriptionService) — ARTIK cüzdan/ledger'dan okur (docs/wallet-redesign.md).
    // ParentSubscription şekli korunur; BestPurchase cüzdandan türetilen sentetik pakettir.
    public class SubscriptionServiceTests
    {
        private static AppDbContext NewContext(string db) =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(db).Options);

        [Fact]
        public async Task ComputeForParents_UsedMinutes_And_Remaining_FromWallet()
        {
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
            {
                c.Customers.Add(new Customer { Id = 1, Name = "Parent" });
                // 180 dk kredi, 30 dk tüketim → bakiye 150.
                c.Wallets.Add(new Wallet { Id = 100, CustomerId = 1, TimeBalanceMinutes = 150, ValidFrom = now.AddDays(-1), ValidTo = now.AddDays(1) });
                c.TimeLedger.Add(new TimeLedgerEntry { WalletId = 100, Type = TimeLedgerType.Credit, MinutesDelta = 180 });
                c.TimeLedger.Add(new TimeLedgerEntry { WalletId = 100, Type = TimeLedgerType.Consumption, MinutesDelta = -30, BookingId = 30 });
                await c.SaveChangesAsync();
            }

            using (var c = NewContext(db))
            {
                var sub = (await new SubscriptionService(c).ComputeForParents(new[] { 1 }))[1];
                Assert.True(sub.HasAny);
                Assert.False(sub.HasUpcoming);
                Assert.True(sub.BestIsDateValid);
                Assert.Equal(30d, sub.BestUsedMinutes, 5);
                Assert.Equal(150d, sub.BestRemainingMinutes, 5);
                Assert.Equal(100, sub.BestPurchase!.Id);
                Assert.Equal(3m, sub.BestPurchase.Hours);        // (150+30)/60 = 3 saat (sentetik)
            }
        }

        [Fact]
        public async Task ComputeForParents_ExpiredWallet_IsNotDateValid()
        {
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
            {
                c.Customers.Add(new Customer { Id = 1, Name = "Parent" });
                // Bakiyesi var ama geçerlilik penceresi geçmişte.
                c.Wallets.Add(new Wallet { Id = 100, CustomerId = 1, TimeBalanceMinutes = 300, ValidFrom = now.AddDays(-10), ValidTo = now.AddDays(-5) });
                c.TimeLedger.Add(new TimeLedgerEntry { WalletId = 100, Type = TimeLedgerType.Credit, MinutesDelta = 300 });
                await c.SaveChangesAsync();
            }

            using (var c = NewContext(db))
            {
                var sub = (await new SubscriptionService(c).ComputeForParents(new[] { 1 }))[1];
                Assert.True(sub.HasAny);
                Assert.False(sub.HasUpcoming);
                Assert.False(sub.BestIsDateValid);               // süresi dolmuş
                Assert.Equal(300d, sub.BestRemainingMinutes, 5);
            }
        }

        [Fact]
        public async Task ComputeForParents_NoWallet_YieldsEmptySubscription()
        {
            var db = Guid.NewGuid().ToString();
            using (var c = NewContext(db))
            {
                c.Customers.Add(new Customer { Id = 7, Name = "Lonely" });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var sub = (await new SubscriptionService(c).ComputeForParents(new[] { 7 }))[7];
                Assert.False(sub.HasAny);
                Assert.Null(sub.BestPurchase);
            }
        }

        [Fact]
        public async Task GetActiveBookings_DerivesActiveSubscriberTier()
        {
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
            {
                c.Customers.Add(new Customer { Id = 1, Name = "Parent" });
                c.Children.Add(new Child { Id = 2, ParentId = 1, Name = "Kid", IsDeleted = false });
                c.Bookings.Add(new Booking { Id = 3, ChildId = 2, Status = BookingStatus.Active });
                c.Wallets.Add(new Wallet { Id = 100, CustomerId = 1, TimeBalanceMinutes = 120, ValidFrom = now.AddDays(-1), ValidTo = now.AddDays(1) });
                c.TimeLedger.Add(new TimeLedgerEntry { WalletId = 100, Type = TimeLedgerType.Credit, MinutesDelta = 120 });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var rows = await new SubscriptionService(c).GetActiveBookings();
                var row = Assert.Single(rows);
                Assert.Equal("ActiveSubscriber", row.Customer!.Status);
                Assert.Equal(2, row.Customer.ChildId);
            }
        }

        [Fact]
        public async Task GetActiveBookings_NoWallet_IsPlainCustomer()
        {
            var db = Guid.NewGuid().ToString();
            using (var c = NewContext(db))
            {
                c.Customers.Add(new Customer { Id = 1, Name = "Parent" });
                c.Children.Add(new Child { Id = 2, ParentId = 1, Name = "Kid", IsDeleted = false });
                c.Bookings.Add(new Booking { Id = 3, ChildId = 2, Status = BookingStatus.Paused });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var rows = await new SubscriptionService(c).GetActiveBookings();
                var row = Assert.Single(rows);
                Assert.Equal("Customer", row.Customer!.Status);
            }
        }
    }
}
