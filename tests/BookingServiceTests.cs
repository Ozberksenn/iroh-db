using Iroh.Exceptions;
using Iroh.Models.DTOs.Booking;
using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Iroh.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Iroh.Tests
{
    // Faz 1 / F1.1 + F1.2 — seans açılış (çocuk zorunlu + tek açık seans) ve
    // update koruma kuralları (tamamlama kilidi, bitmiş-seans dokunulmazlığı, geçiş matrisi).
    // NOT: InMemory transaction/unique-index enforce etmez → burada servis-seviyesi kontroller test edilir.
    public class BookingServiceTests
    {
        private const int GuestChildId = 999999;

        private static AppDbContext NewContext(string db) =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(db).Options);

        // ---- F1.1: çocuk zorunlu ----
        [Fact]
        public async Task Create_Throws_WhenChildIdNull()
        {
            using var c = NewContext(Guid.NewGuid().ToString());
            var svc = new BookingService(c);

            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.Create(new Booking { ChildId = null, Status = BookingStatus.Active }));
        }

        // ---- F1.1: bir çocuk = tek açık seans ----
        [Fact]
        public async Task Create_Throws_WhenChildAlreadyHasOpenSession()
        {
            var db = Guid.NewGuid().ToString();
            using (var c = NewContext(db))
            {
                c.Bookings.Add(new Booking { Id = 1, ChildId = 5, Status = BookingStatus.Active });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var svc = new BookingService(c);
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    svc.Create(new Booking { Id = 2, ChildId = 5, Status = BookingStatus.Active }));
            }
        }

        [Fact]
        public async Task Create_Throws_WhenChildHasPausedSession()
        {
            var db = Guid.NewGuid().ToString();
            using (var c = NewContext(db))
            {
                c.Bookings.Add(new Booking { Id = 1, ChildId = 5, Status = BookingStatus.Paused });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var svc = new BookingService(c);
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    svc.Create(new Booking { Id = 2, ChildId = 5, Status = BookingStatus.Active }));
            }
        }

        [Fact]
        public async Task Create_Allows_MultipleOpenSessions_ForGuestChild()
        {
            var db = Guid.NewGuid().ToString();
            using (var c = NewContext(db))
            {
                c.Bookings.Add(new Booking { Id = 1, ChildId = GuestChildId, Status = BookingStatus.Active });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var svc = new BookingService(c);
                var created = await svc.Create(new Booking { Id = 2, ChildId = GuestChildId, Status = BookingStatus.Active });
                Assert.Equal(BookingStatus.Active, created.Status);
                Assert.Equal(2, await c.Bookings.CountAsync(b => b.ChildId == GuestChildId));
            }
        }

        [Fact]
        public async Task Create_Succeeds_WhenNoOpenSession()
        {
            var db = Guid.NewGuid().ToString();
            // Aynı çocuğun KAPALI (Completed) geçmiş seansı yeni açılışı engellememeli.
            using (var c = NewContext(db))
            {
                c.Bookings.Add(new Booking { Id = 1, ChildId = 7, Status = BookingStatus.Completed });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var svc = new BookingService(c);
                var created = await svc.Create(new Booking { Id = 2, ChildId = 7, Status = BookingStatus.Active });
                Assert.NotNull(await c.Bookings.FindAsync(created.Id));
            }
        }

        // ---- F1.2: tamamlama kilidi ----
        [Fact]
        public async Task Update_Throws_WhenCompletingOpenSession()
        {
            var db = Guid.NewGuid().ToString();
            using (var c = NewContext(db))
            {
                c.Bookings.Add(new Booking { Id = 1, ChildId = 5, Status = BookingStatus.Active });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var svc = new BookingService(c);
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    svc.Update(1, new BookingUpdateDto { Id = 1, Status = BookingStatus.Completed }));
            }
        }

        // ---- F1.2: bitmiş seans dokunulmazlığı ----
        [Fact]
        public async Task Update_Throws_WhenChangingStatusOfFinishedSession()
        {
            var db = Guid.NewGuid().ToString();
            using (var c = NewContext(db))
            {
                c.Bookings.Add(new Booking { Id = 1, ChildId = 5, Status = BookingStatus.Completed });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var svc = new BookingService(c);
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    svc.Update(1, new BookingUpdateDto { Id = 1, Status = BookingStatus.Active }));
            }
        }

        [Fact]
        public async Task Update_AllowsNoteEdit_OnFinishedSession()
        {
            var db = Guid.NewGuid().ToString();
            using (var c = NewContext(db))
            {
                c.Bookings.Add(new Booking { Id = 1, ChildId = 5, Status = BookingStatus.Canceled, Note = "eski" });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var svc = new BookingService(c);
                var updated = await svc.Update(1, new BookingUpdateDto { Id = 1, Status = BookingStatus.Canceled, Note = "yeni" });
                Assert.Equal("yeni", updated.Note);
                Assert.Equal(BookingStatus.Canceled, updated.Status);
            }
        }

        // ---- F1.2: izinli geçiş ----
        [Fact]
        public async Task Update_Allows_ActiveToPaused()
        {
            var db = Guid.NewGuid().ToString();
            using (var c = NewContext(db))
            {
                c.Bookings.Add(new Booking { Id = 1, ChildId = 5, Status = BookingStatus.Active });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var svc = new BookingService(c);
                var updated = await svc.Update(1, new BookingUpdateDto { Id = 1, Status = BookingStatus.Paused });
                Assert.Equal(BookingStatus.Paused, updated.Status);
            }
        }

        // ---- F2.5: atomik durum değişimi (ChangeStatus) ----
        [Fact]
        public async Task ChangeStatus_Pause_SetsPaused_AndWritesLog()
        {
            var db = Guid.NewGuid().ToString();
            using (var c = NewContext(db))
            {
                c.Bookings.Add(new Booking { Id = 1, ChildId = 5, Status = BookingStatus.Active });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var svc = new BookingService(c);
                var b = await svc.ChangeStatus(1, BookingStatus.Paused, BookingLogType.Pause, 0, null, 7);
                Assert.Equal(BookingStatus.Paused, b.Status);
                var log = await c.BookingLogs.SingleAsync(l => l.BookingId == 1);
                Assert.Equal(BookingLogType.Pause, log.Type);
                Assert.Equal(7, log.UserId);
            }
        }

        [Fact]
        public async Task ChangeStatus_Cancel_SetsEndTime_AndLog()
        {
            var db = Guid.NewGuid().ToString();
            using (var c = NewContext(db))
            {
                c.Bookings.Add(new Booking { Id = 1, ChildId = 5, Status = BookingStatus.Paused });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var svc = new BookingService(c);
                var b = await svc.ChangeStatus(1, BookingStatus.Canceled, BookingLogType.Cancel, 0, "iptal notu", 7);
                Assert.Equal(BookingStatus.Canceled, b.Status);
                Assert.NotNull(b.EndTime);
                Assert.Equal("iptal notu", b.Note);
                Assert.Equal(1, await c.BookingLogs.CountAsync(l => l.BookingId == 1 && l.Type == BookingLogType.Cancel));
            }
        }

        [Fact]
        public async Task ChangeStatus_Pause_OnAlreadyPaused_Throws()
        {
            var db = Guid.NewGuid().ToString();
            using (var c = NewContext(db))
            {
                c.Bookings.Add(new Booking { Id = 1, ChildId = 5, Status = BookingStatus.Paused });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var svc = new BookingService(c);
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    svc.ChangeStatus(1, BookingStatus.Paused, BookingLogType.Pause, 0, null, 7));
            }
        }

        [Fact]
        public async Task ChangeStatus_OnFinished_Throws()
        {
            var db = Guid.NewGuid().ToString();
            using (var c = NewContext(db))
            {
                c.Bookings.Add(new Booking { Id = 1, ChildId = 5, Status = BookingStatus.Completed });
                await c.SaveChangesAsync();
            }
            using (var c = NewContext(db))
            {
                var svc = new BookingService(c);
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    svc.ChangeStatus(1, BookingStatus.Canceled, BookingLogType.Cancel, 0, null, 7));
            }
        }
    }
}
