using Iroh.Models.DTOs.Booking;
using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    // Billing / abonelik çekirdeği — vw_activebookings + fn_search_unified + fn_get_used_hours mantığının ortak C# karşılığı.
    public interface ISubscriptionService
    {
        Task<Dictionary<int, SubscriptionService.ParentSubscription>> ComputeForParents(IReadOnlyCollection<int> parentIds);
        Task<List<ActiveBookingDto>> GetActiveBookings();
    }

    public class SubscriptionService : ISubscriptionService
    {
        private readonly AppDbContext _context;
        public SubscriptionService(AppDbContext context)
        {
            _context = context;
        }

        // Bir ebeveynin "en iyi paketi" + ham abonelik gerçekleri. Statü kademesini çağıran belirler
        // (active-bookings 5 kademe, search-unified 4 kademe — farklı, o yüzden burada ham bırakılıyor).
        public sealed class ParentSubscription
        {
            public PurchaseInfoDto? BestPurchase { get; init; }   // cüzdandan türetilen sentetik paket (entity değil)
            public double BestRemainingMinutes { get; init; }
            public bool BestIsDateValid { get; init; }
            public double BestUsedMinutes { get; init; }
            public bool HasUpcoming { get; init; }
            public bool HasAny { get; init; }
        }

        // Cüzdandan okur (docs/wallet-redesign.md): müşteri başına tek bakiye + geçerlilik.
        // ParentSubscription şekli korunur; BestPurchase, cüzdandan türetilen SENTETİK bir paket
        // (eski "en iyi paket" tahmini kalktı). Tüketim/kredi tek doğruluk noktası = ledger.
        public async Task<Dictionary<int, ParentSubscription>> ComputeForParents(IReadOnlyCollection<int> parentIds)
        {
            var now = DateTime.UtcNow;
            var result = new Dictionary<int, ParentSubscription>();
            if (parentIds.Count == 0) return result;

            var wallets = await _context.Wallets.AsNoTracking()
                .Where(w => parentIds.Contains(w.CustomerId)).ToListAsync();
            var walletIds = wallets.Select(w => w.Id).ToList();
            var ledger = await _context.TimeLedger.AsNoTracking()
                .Where(e => walletIds.Contains(e.WalletId)).ToListAsync();

            foreach (var pid in parentIds)
            {
                var wallet = wallets.FirstOrDefault(w => w.CustomerId == pid);
                if (wallet == null)
                {
                    result[pid] = new ParentSubscription();
                    continue;
                }

                var rows = ledger.Where(e => e.WalletId == wallet.Id).ToList();
                var used = -rows.Where(e => e.Type == Models.Enums.TimeLedgerType.Consumption).Sum(e => e.MinutesDelta); // pozitif dk
                var hasCredit = rows.Any(e => e.Type == Models.Enums.TimeLedgerType.Credit);
                var isValid = wallet.ValidFrom.HasValue && wallet.ValidTo.HasValue
                              && wallet.ValidFrom.Value <= now && wallet.ValidTo.Value >= now;
                var hasUpcoming = wallet.ValidFrom.HasValue && wallet.ValidFrom.Value > now;
                // total - used == bakiye olacak şekilde (client hours*60 - usedHours hesabıyla uyumlu).
                var totalAvail = wallet.TimeBalanceMinutes + used;

                result[pid] = new ParentSubscription
                {
                    BestPurchase = new PurchaseInfoDto
                    {
                        Id = wallet.Id,
                        CustomerId = pid,
                        Hours = totalAvail / 60m,
                        Price = 0m,
                        StartDate = wallet.ValidFrom,
                        EndDate = wallet.ValidTo,
                        UsedHours = used,
                        Payments = new()
                    },
                    BestRemainingMinutes = wallet.TimeBalanceMinutes,
                    BestIsDateValid = isValid,
                    BestUsedMinutes = used,
                    HasUpcoming = hasUpcoming,
                    HasAny = hasCredit
                };
            }

            return result;
        }

        // vw_activebookings: Active/Paused oturumlar + ebeveyn başına en iyi paket, abone kademesi (5'li), usedMinutes, payments, logs.
        public async Task<List<ActiveBookingDto>> GetActiveBookings()
        {
            var bookings = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.Table)
                .Include(b => b.Child).ThenInclude(ch => ch.Parent)
                .Include(b => b.Logs)
                .Where(b => (b.Status == BookingStatus.Active || b.Status == BookingStatus.Paused)
                         && (b.Child == null || !b.Child.IsDeleted)
                         && (b.Child == null || b.Child.Parent == null || !b.Child.Parent.IsDeleted))
                .OrderBy(b => b.Id)
                .ToListAsync();

            var parentIds = bookings
                .Where(b => b.Child != null && b.Child.Parent != null)
                .Select(b => b.Child!.Parent!.Id)
                .Distinct()
                .ToList();

            var subs = await ComputeForParents(parentIds);

            return bookings.Select(b =>
            {
                ActiveBookingCustomerDto? customer = null;
                if (b.Child != null)
                {
                    var parent = b.Child.Parent;
                    var tier = "Customer";
                    PurchaseInfoDto? pinfo = null;
                    if (parent != null && subs.TryGetValue(parent.Id, out var sub))
                    {
                        // Tek statü fonksiyonu (docs/wallet-redesign.md §3) — eski 5 dalın birebir karşılığı.
                        tier = WalletService.Derive((int)sub.BestRemainingMinutes, sub.BestIsDateValid, sub.HasUpcoming, sub.HasAny).ToString();

                        pinfo = sub.BestPurchase;   // cüzdandan türetilen sentetik paket (ComputeForParents)
                    }
                    customer = new ActiveBookingCustomerDto
                    {
                        Id = b.Child.Id,
                        ChildId = b.Child.Id,
                        Name = b.Child.Name,
                        ParentId = parent?.Id,
                        ParentName = parent?.Name,
                        ParentLastName = parent?.LastName,
                        Phone = parent?.Phone,
                        Status = tier,
                        Purchase = pinfo
                    };
                }

                return new ActiveBookingDto
                {
                    Id = b.Id,
                    TableId = b.TableId,
                    ChildId = b.Child?.Id,
                    CustomerId = b.Child?.Parent?.Id,
                    Table = b.TableId != null ? new BookingTableDto { Id = b.Table?.Id, Name = b.Table?.Name } : null,
                    Customer = customer,
                    Price = b.Price,
                    StartTime = b.StartTime,
                    EndTime = b.EndTime,
                    SubscriptionStartTime = b.SubscriptionStartTime,
                    SubscriptionEndTime = b.SubscriptionEndTime,
                    Status = b.Status.ToString(),
                    Note = b.Note,
                    Logs = b.Logs
                        .OrderBy(l => l.Id)
                        .Select(l => new BookingLogDto { Id = l.Id, BookingId = l.BookingId, Time = l.Time, Type = l.Type.ToString(), UserId = l.UserId })
                        .ToList()
                };
            }).ToList();
        }
    }
}
