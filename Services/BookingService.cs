using Iroh.Domain;
using Iroh.Models.DTOs.Booking;
using Iroh.Models.DTOs.Common;
using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Iroh.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public interface IBookingService
    {
        Task<PagedResult<BookingListItemDto>> GetBookings(
            int page, int size, string[]? status, string? name, string? mail,
            int? customerId, int? childId, DateTime? startTime, DateTime? endTime, int? tableId);
        Task<Booking?> GetById(int id);
        Task<Booking> Create(Booking booking);
        Task<Booking> Update(int id, BookingUpdateDto updatedBooking);
        // F2.5: durum değişimi + geçmiş log'u TEK transaction'da (atomik). userId JWT'den gelir.
        Task<Booking> ChangeStatus(int id, BookingStatus target, BookingLogType logType, int? minutesAgo, string? note, int? userId);
    }

    public class BookingService : IBookingService
    {
        private readonly AppDbContext _context;
        public BookingService(AppDbContext context)
        {
            _context = context;
        }

        // usp_get_bookings paritesi: sayfalı + filtreli liste; table/customer iç içe DTO; ORDER BY id DESC.
        public async Task<PagedResult<BookingListItemDto>> GetBookings(
            int page, int size, string[]? status, string? name, string? mail,
            int? customerId, int? childId, DateTime? startTime, DateTime? endTime, int? tableId)
        {
            // NOT: p_mail proc imzasında var ama gövdesinde kullanılmıyor (no-op) — parite için yok sayıldı.
            var query = _context.Bookings.AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                var pattern = $"%{name}%";
                query = query.Where(b => EF.Functions.ILike(
                    (b.Child != null ? b.Child.Name : "") + " " +
                    (b.Child != null && b.Child.Parent != null ? b.Child.Parent.Name : "") + " " +
                    (b.Child != null && b.Child.Parent != null ? (b.Child.Parent.LastName ?? "") : "") + " " +
                    (b.Child != null && b.Child.Parent != null ? (b.Child.Parent.Phone ?? "") : "") + " " +
                    (b.Table != null ? (b.Table.Name ?? "") : "") + " " +
                    (b.Note ?? ""),
                    pattern));
            }

            if (status != null && status.Length > 0)
            {
                var statusEnums = status
                    .Select(s => Enum.TryParse<BookingStatus>(s, true, out var e) ? (BookingStatus?)e : null)
                    .Where(e => e.HasValue)
                    .Select(e => e!.Value)
                    .ToList();
                if (statusEnums.Count > 0)
                {
                    query = query.Where(b => statusEnums.Contains(b.Status));
                }
            }

            if (customerId.HasValue)
                query = query.Where(b => b.Child != null && b.Child.ParentId == customerId.Value);
            if (childId.HasValue)
                query = query.Where(b => b.ChildId == childId.Value);
            if (startTime.HasValue)
                query = query.Where(b => b.StartTime >= startTime.Value);
            if (endTime.HasValue)
                query = query.Where(b => b.StartTime <= endTime.Value);
            if (tableId.HasValue)
                query = query.Where(b => b.TableId == tableId.Value);

            var totalSize = await query.CountAsync();

            IQueryable<Booking> ordered = query.OrderByDescending(b => b.Id);
            if (page != -1)
                ordered = ordered.Skip((page - 1) * size).Take(size);

            var items = await ordered.Select(b => new BookingListItemDto
            {
                Id = b.Id,
                Table = b.TableId != null
                    ? new BookingTableDto { Id = b.Table!.Id, Name = b.Table.Name }
                    : null,
                Customer = b.ChildId != null
                    ? new BookingCustomerDto
                    {
                        ChildId = b.Child!.Id,
                        Name = b.Child.Name,
                        ParentId = b.Child.Parent != null ? b.Child.Parent.Id : (int?)null,
                        ParentName = b.Child.Parent != null ? b.Child.Parent.Name : null,
                        ParentLastName = b.Child.Parent != null ? b.Child.Parent.LastName : null,
                        Phone = b.Child.Parent != null ? b.Child.Parent.Phone : null
                    }
                    : null,
                Price = b.Price,
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                Status = b.Status.ToString(),
                Note = b.Note
            }).ToListAsync();

            // Kapanış kırılımı (gerçek kaynak = ledger): sayfadaki booking'ler için tek GroupBy sorgusu,
            // sonra bellekte eşleştir. Abonelikten düşülen (Consumption, negatif) + borca yazılan (DebtCharge).
            var bookingIds = items.Select(i => i.Id).ToList();
            if (bookingIds.Count > 0)
            {
                var ledgerAgg = await _context.TimeLedger
                    .Where(e => e.BookingId.HasValue && bookingIds.Contains(e.BookingId.Value)
                                && (e.Type == TimeLedgerType.Consumption || e.Type == TimeLedgerType.DebtCharge))
                    .GroupBy(e => new { BookingId = e.BookingId!.Value, e.Type })
                    .Select(g => new { g.Key.BookingId, g.Key.Type, Minutes = g.Sum(x => x.MinutesDelta) })
                    .ToListAsync();

                foreach (var it in items)
                {
                    it.CoveredMinutes = -ledgerAgg
                        .Where(a => a.BookingId == it.Id && a.Type == TimeLedgerType.Consumption)
                        .Sum(a => a.Minutes);   // Consumption negatif → pozitife çevir
                    it.DebtedMinutes = ledgerAgg
                        .Where(a => a.BookingId == it.Id && a.Type == TimeLedgerType.DebtCharge)
                        .Sum(a => a.Minutes);
                }
            }

            // Müşteri rozeti per-session ledger'a DEĞİL, parent'ın abonelik geçmişine (hasAny) bağlıdır:
            // abonenin nakit-ödenmiş oturumu da "Abone" görünmeli. hasAny = cüzdanda en az bir Credit.
            var parentIds = items.Where(i => i.Customer?.ParentId != null)
                .Select(i => i.Customer!.ParentId!.Value).Distinct().ToList();
            if (parentIds.Count > 0)
            {
                var subscriberParents = (await _context.Wallets
                    .Where(w => parentIds.Contains(w.CustomerId)
                        && _context.TimeLedger.Any(e => e.WalletId == w.Id && e.Type == TimeLedgerType.Credit))
                    .Select(w => w.CustomerId)
                    .ToListAsync()).ToHashSet();
                foreach (var it in items)
                    if (it.Customer?.ParentId != null)
                        it.Customer.IsSubscriber = subscriberParents.Contains(it.Customer.ParentId.Value);
            }

            return new PagedResult<BookingListItemDto>
            {
                Items = items,
                Page = page,
                Size = size,
                TotalSize = totalSize,
                TotalPages = page == -1 ? 1 : (int)Math.Ceiling((double)totalSize / size)
            };
        }

        public async Task<Booking?> GetById(int id) =>
            await _context.Bookings
                .AsNoTracking()
                .Include(b => b.Table)
                .Include(b => b.Child)
                    .ThenInclude(c => c.Parent)
                .FirstOrDefaultAsync(b => b.Id == id);

        public async Task<Booking> Create(Booking booking)
        {
            // F1.1: Çocuk zorunlu — çocuksuz seans açılamaz (client de engeller, server kesin garanti).
            if (booking.ChildId == null)
                throw new BusinessRuleException("Çocuk seçimi zorunludur.");

            // F1.1: Bir çocuk = en fazla bir açık (Active/Paused) seans. Sistem Misafiri (999999) muaf.
            if (booking.ChildId.Value != SystemConstants.GuestCustomerId)
            {
                var childId = booking.ChildId.Value;
                var hasOpen = await _context.Bookings.AnyAsync(b =>
                    b.ChildId == childId &&
                    (b.Status == BookingStatus.Active || b.Status == BookingStatus.Paused));
                if (hasOpen)
                    throw new BusinessRuleException("Bu çocuğun zaten açık bir seansı var.");
            }

            _context.Bookings.Add(booking);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Yarış: iki cihaz aynı anda açtı; ux_one_open_booking_per_child kısmi unique index yakaladı.
                throw new BusinessRuleException("Bu çocuğun zaten açık bir seansı var.");
            }
            return booking;
        }

        public async Task<Booking> Update(int id, BookingUpdateDto updatedBooking)
        {
            var existingBooking = await _context.Bookings.FindAsync(id);
            if (existingBooking == null)
            {
                throw new NotFoundException("Kayıt bulunamadı");
            }

            // F1.2: Bitmiş (Completed/Canceled) seans dokunulmazdır — yalnızca not düzenlenebilir.
            var isFinished = existingBooking.Status == BookingStatus.Completed
                          || existingBooking.Status == BookingStatus.Canceled;
            if (isFinished)
            {
                if (updatedBooking.Status != existingBooking.Status)
                    throw new BusinessRuleException("Tamamlanmış veya iptal edilmiş seans değiştirilemez.");
                existingBooking.Note = updatedBooking.Note ?? existingBooking.Note;
                await _context.SaveChangesAsync();
                return existingBooking;
            }

            // F1.2: Tamamlama yalnızca ödeme akışıyla (kapatma) yapılır; generic update ile "Completed" yasak.
            if (updatedBooking.Status == BookingStatus.Completed)
                throw new BusinessRuleException("Tamamlama yalnızca ödeme akışıyla (kapatma) yapılır.");

            // F1.2: Açık seans için izinli durumlar Active / Paused / Canceled.
            if (updatedBooking.Status != BookingStatus.Active
                && updatedBooking.Status != BookingStatus.Paused
                && updatedBooking.Status != BookingStatus.Canceled)
                throw new BusinessRuleException("Geçersiz seans durumu geçişi.");

            existingBooking.TableId = updatedBooking.TableId ?? existingBooking.TableId;
            existingBooking.StartTime = updatedBooking.StartTime ?? existingBooking.StartTime;
            existingBooking.EndTime = updatedBooking.EndTime ?? existingBooking.EndTime;
            existingBooking.Status = updatedBooking.Status;
            existingBooking.Price = updatedBooking.Price ?? existingBooking.Price;
            existingBooking.ChildId = updatedBooking.ChildId ?? existingBooking.ChildId;
            existingBooking.Note = updatedBooking.Note ?? existingBooking.Note;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Çocuk değiştirilip zaten açık seansı olan birine atanırsa kısmi unique index yakalar.
                throw new BusinessRuleException("Bu çocuğun zaten açık bir seansı var.");
            }

            return existingBooking;
        }

        // F2.5: pause/resume/cancel — durum geçişini doğrula, durum + BookingLog'u TEK transaction'da yaz.
        public async Task<Booking> ChangeStatus(int id, BookingStatus target, BookingLogType logType,
            int? minutesAgo, string? note, int? userId)
        {
            var booking = await _context.Bookings.FindAsync(id)
                ?? throw new NotFoundException("Kayıt bulunamadı");

            // Bitmiş seans değiştirilemez (F1.2 ile aynı kural).
            if (booking.Status == BookingStatus.Completed || booking.Status == BookingStatus.Canceled)
                throw new BusinessRuleException("Tamamlanmış veya iptal edilmiş seans değiştirilemez.");

            // Geçiş matrisi: duraklat Active→Paused, devam Paused→Active, iptal Active/Paused→Canceled.
            var ok = target switch
            {
                BookingStatus.Paused => booking.Status == BookingStatus.Active,
                BookingStatus.Active => booking.Status == BookingStatus.Paused,
                BookingStatus.Canceled => booking.Status is BookingStatus.Active or BookingStatus.Paused,
                _ => false
            };
            if (!ok)
                throw new BusinessRuleException("Geçersiz seans durumu geçişi.");

            var time = DateTime.UtcNow.AddMinutes(-(minutesAgo ?? 0));

            return await InTransaction(async () =>
            {
                booking.Status = target;
                if (!string.IsNullOrWhiteSpace(note)) booking.Note = note;
                if (target == BookingStatus.Canceled) booking.EndTime = time;

                _context.BookingLogs.Add(new BookingLog
                {
                    BookingId = booking.Id,
                    Time = time,
                    Type = logType,
                    UserId = userId
                });

                await _context.SaveChangesAsync();
                return booking;
            });
        }

        // Mutasyonu transaction içinde çalıştırır. InMemory (testler) transaction desteklemez → doğrudan koşar.
        private async Task<T> InTransaction<T>(Func<Task<T>> action)
        {
            if (!_context.Database.IsRelational())
                return await action();

            await using var tx = await _context.Database.BeginTransactionAsync();
            var result = await action();
            await tx.CommitAsync();
            return result;
        }

        // Postgres unique-constraint ihlali (23505) tespiti — yarış durumlarında dostane hataya çevirmek için.
        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
    }
}
