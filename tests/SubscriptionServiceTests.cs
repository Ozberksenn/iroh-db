using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Iroh.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Iroh.Tests
{
    // Billing çekirdeği (SubscriptionService) — proc fn_get_used_hours + parent_best_package mantığının C# karşılığı.
    // EF InMemory ile test edilebilir: sorgular Where/Join/Include kullanır, ILike YOK.
    public class SubscriptionServiceTests
    {
        private static AppDbContext NewContext(string db) =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(db).Options);

        [Fact]
        public async Task ComputeForParents_UsedMinutes_And_Remaining_Are_Correct()
        {
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
            {
                c.Customers.Add(new Customer { Id = 1, Name = "Parent" });
                // 2 saat paket + 1 saat ek ödeme = (2+1)*60 = 180 dk toplam.
                c.Purchases.Add(new Purchase { Id = 10, CustomerId = 1, Hours = 2m, Price = 100m, CreatedAt = now, StartDate = now.AddDays(-1), EndDate = now.AddDays(1) });
                c.PurchasePayments.Add(new PurchasePayment { Id = 20, PurchaseId = 10, Hours = 1m, Price = 50m });
                // 30 dk kullanılmış oturum.
                c.Bookings.Add(new Booking { Id = 30, Status = BookingStatus.Completed, SubscriptionStartTime = now, SubscriptionEndTime = now.AddMinutes(30) });
                c.PurchaseBookings.Add(new PurchaseBooking { Id = 40, PurchaseId = 10, BookingId = 30 });
                await c.SaveChangesAsync();
            }

            using (var c = NewContext(db))
            {
                var svc = new SubscriptionService(c);
                var result = await svc.ComputeForParents(new[] { 1 });

                Assert.True(result.ContainsKey(1));
                var sub = result[1];
                Assert.True(sub.HasAny);
                Assert.False(sub.HasUpcoming);
                Assert.True(sub.BestIsDateValid);
                Assert.Equal(30d, sub.BestUsedMinutes, 5);
                Assert.Equal(150d, sub.BestRemainingMinutes, 5); // 180 - 30
                Assert.Equal(10, sub.BestPurchase!.Id);
            }
        }

        [Fact]
        public async Task ComputeForParents_PrefersValidPackage_OverExpired()
        {
            var db = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            using (var c = NewContext(db))
            {
                c.Customers.Add(new Customer { Id = 1, Name = "Parent" });
                // Süresi geçmiş ama büyük paket.
                c.Purchases.Add(new Purchase { Id = 10, CustomerId = 1, Hours = 5m, Price = 1m, CreatedAt = now, StartDate = now.AddDays(-10), EndDate = now.AddDays(-5) });
                // Geçerli ve bakiyesi olan paket.
                c.Purchases.Add(new Purchase { Id = 11, CustomerId = 1, Hours = 1m, Price = 1m, CreatedAt = now, StartDate = now.AddDays(-1), EndDate = now.AddDays(1) });
                await c.SaveChangesAsync();
            }

            using (var c = NewContext(db))
            {
                var sub = (await new SubscriptionService(c).ComputeForParents(new[] { 1 }))[1];
                Assert.True(sub.BestIsDateValid);
                Assert.Equal(11, sub.BestPurchase!.Id);          // geçerli olan seçilmeli
                Assert.Equal(60d, sub.BestRemainingMinutes, 5);  // 1 saat = 60 dk, kullanım yok
            }
        }

        [Fact]
        public async Task ComputeForParents_NoPurchases_YieldsEmptySubscription()
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
                c.Purchases.Add(new Purchase { Id = 10, CustomerId = 1, Hours = 2m, Price = 1m, CreatedAt = now, StartDate = now.AddDays(-1), EndDate = now.AddDays(1) });
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
        public async Task GetActiveBookings_NoPurchase_IsPlainCustomer()
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
