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
            public Purchase? BestPurchase { get; init; }
            public double BestRemainingMinutes { get; init; }
            public bool BestIsDateValid { get; init; }
            public double BestUsedMinutes { get; init; }
            public List<PurchasePayment> BestPayments { get; init; } = new();
            public bool HasUpcoming { get; init; }
            public bool HasAny { get; init; }
        }

        // parent_best_package + fn_get_used_hours: verilen ebeveynler için en iyi paketi ve abonelik gerçeklerini hesaplar.
        public async Task<Dictionary<int, ParentSubscription>> ComputeForParents(IReadOnlyCollection<int> parentIds)
        {
            var now = DateTime.UtcNow;
            var result = new Dictionary<int, ParentSubscription>();
            if (parentIds.Count == 0) return result;

            var purchases = await _context.Purchases.AsNoTracking().Where(p => parentIds.Contains(p.CustomerId)).ToListAsync();
            var purchaseIds = purchases.Select(p => p.Id).ToList();
            var payments = await _context.PurchasePayments.AsNoTracking().Where(pp => purchaseIds.Contains(pp.PurchaseId)).ToListAsync();
            var linked = await _context.PurchaseBookings
                .Where(pb => purchaseIds.Contains(pb.PurchaseId))
                .Join(_context.Bookings, pb => pb.BookingId, b => b.Id,
                      (pb, b) => new { pb.PurchaseId, b.SubscriptionStartTime, b.SubscriptionEndTime })
                .ToListAsync();

            // fn_get_used_hours: Σ(subEnd - subStart) dakika cinsinden.
            double UsedMinutesFor(int pid) => linked
                .Where(x => x.PurchaseId == pid && x.SubscriptionStartTime.HasValue && x.SubscriptionEndTime.HasValue)
                .Sum(x => (x.SubscriptionEndTime!.Value - x.SubscriptionStartTime!.Value).TotalMinutes);

            foreach (var pid in parentIds)
            {
                var cust = purchases.Where(p => p.CustomerId == pid).ToList();
                if (cust.Count == 0)
                {
                    result[pid] = new ParentSubscription();
                    continue;
                }

                var evals = cust.Select(p =>
                {
                    var payHours = payments.Where(pp => pp.PurchaseId == p.Id).Sum(pp => (double)pp.Hours);
                    var used = UsedMinutesFor(p.Id);
                    var totalMin = ((double)p.Hours + payHours) * 60.0;
                    var isValid = p.StartDate.HasValue && p.EndDate.HasValue && p.StartDate.Value <= now && p.EndDate.Value >= now;
                    return new { p, used, rem = totalMin - used, isValid };
                }).ToList();

                // DISTINCT ON (customerid) ORDER BY is_date_valid DESC, (rem>0) DESC, enddate DESC
                var best = evals
                    .OrderByDescending(e => e.isValid)
                    .ThenByDescending(e => e.rem > 0)
                    .ThenByDescending(e => e.p.EndDate ?? DateTime.MinValue)
                    .First();

                result[pid] = new ParentSubscription
                {
                    BestPurchase = best.p,
                    BestRemainingMinutes = best.rem,
                    BestIsDateValid = best.isValid,
                    BestUsedMinutes = best.used,
                    BestPayments = payments.Where(pp => pp.PurchaseId == best.p.Id).ToList(),
                    HasUpcoming = cust.Any(p => p.StartDate.HasValue && p.StartDate.Value > now),
                    HasAny = true
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
                        // active-bookings kademesi (5'li): Subscriber dahil.
                        tier = (sub.BestIsDateValid && sub.BestRemainingMinutes > 0) ? "ActiveSubscriber"
                             : sub.BestIsDateValid ? "OverageSubscriber"
                             : sub.HasUpcoming ? "UpcomingSubscriber"
                             : sub.HasAny ? "Subscriber"
                             : "Customer";

                        if (sub.BestPurchase != null)
                        {
                            pinfo = new PurchaseInfoDto
                            {
                                Id = sub.BestPurchase.Id,
                                Hours = sub.BestPurchase.Hours,
                                Price = sub.BestPurchase.Price,
                                StartDate = sub.BestPurchase.StartDate,
                                EndDate = sub.BestPurchase.EndDate,
                                CustomerId = sub.BestPurchase.CustomerId,
                                UsedHours = sub.BestUsedMinutes,
                                Payments = sub.BestPayments
                                    .Select(pp => new PaymentDto { Id = pp.Id, PurchaseId = pp.PurchaseId, Hours = pp.Hours, Price = pp.Price })
                                    .ToList()
                            };
                        }
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
