using Iroh.Models.DTOs.Booking;
using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    // Billing / abonelik çekirdeği — vw_activebookings + fn_search_unified + fn_get_used_hours mantığının ortak C# karşılığı.
    public class SubscriptionService
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

            var purchases = await _context.Purchases.AsNoTracking().Where(p => parentIds.Contains(p.customerId)).ToListAsync();
            var purchaseIds = purchases.Select(p => p.id).ToList();
            var payments = await _context.PurchasePayments.AsNoTracking().Where(pp => purchaseIds.Contains(pp.purchaseId)).ToListAsync();
            var linked = await _context.PurchaseBookings
                .Where(pb => purchaseIds.Contains(pb.purchaseId))
                .Join(_context.Bookings, pb => pb.bookingId, b => b.id,
                      (pb, b) => new { pb.purchaseId, b.subscriptionStartTime, b.subscriptionEndTime })
                .ToListAsync();

            // fn_get_used_hours: Σ(subEnd - subStart) dakika cinsinden.
            double UsedMinutesFor(int pid) => linked
                .Where(x => x.purchaseId == pid && x.subscriptionStartTime.HasValue && x.subscriptionEndTime.HasValue)
                .Sum(x => (x.subscriptionEndTime!.Value - x.subscriptionStartTime!.Value).TotalMinutes);

            foreach (var pid in parentIds)
            {
                var cust = purchases.Where(p => p.customerId == pid).ToList();
                if (cust.Count == 0)
                {
                    result[pid] = new ParentSubscription();
                    continue;
                }

                var evals = cust.Select(p =>
                {
                    var payHours = payments.Where(pp => pp.purchaseId == p.id).Sum(pp => (double)pp.hours);
                    var used = UsedMinutesFor(p.id);
                    var totalMin = ((double)p.hours + payHours) * 60.0;
                    var isValid = p.startDate.HasValue && p.endDate.HasValue && p.startDate.Value <= now && p.endDate.Value >= now;
                    return new { p, used, rem = totalMin - used, isValid };
                }).ToList();

                // DISTINCT ON (customerid) ORDER BY is_date_valid DESC, (rem>0) DESC, enddate DESC
                var best = evals
                    .OrderByDescending(e => e.isValid)
                    .ThenByDescending(e => e.rem > 0)
                    .ThenByDescending(e => e.p.endDate ?? DateTime.MinValue)
                    .First();

                result[pid] = new ParentSubscription
                {
                    BestPurchase = best.p,
                    BestRemainingMinutes = best.rem,
                    BestIsDateValid = best.isValid,
                    BestUsedMinutes = best.used,
                    BestPayments = payments.Where(pp => pp.purchaseId == best.p.id).ToList(),
                    HasUpcoming = cust.Any(p => p.startDate.HasValue && p.startDate.Value > now),
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
                .Include(b => b.table)
                .Include(b => b.child).ThenInclude(ch => ch.parent)
                .Include(b => b.logs)
                .Where(b => (b.status == BookingStatus.Active || b.status == BookingStatus.Paused)
                         && (b.child == null || !b.child.isDeleted)
                         && (b.child == null || b.child.parent == null || !b.child.parent.isDeleted))
                .OrderBy(b => b.id)
                .ToListAsync();

            var parentIds = bookings
                .Where(b => b.child != null && b.child.parent != null)
                .Select(b => b.child!.parent!.id)
                .Distinct()
                .ToList();

            var subs = await ComputeForParents(parentIds);

            return bookings.Select(b =>
            {
                ActiveBookingCustomerDto? customer = null;
                if (b.child != null)
                {
                    var parent = b.child.parent;
                    var tier = "Customer";
                    PurchaseInfoDto? pinfo = null;
                    if (parent != null && subs.TryGetValue(parent.id, out var sub))
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
                                id = sub.BestPurchase.id,
                                hours = sub.BestPurchase.hours,
                                price = sub.BestPurchase.price,
                                startDate = sub.BestPurchase.startDate,
                                endDate = sub.BestPurchase.endDate,
                                customerId = sub.BestPurchase.customerId,
                                usedMinutes = sub.BestUsedMinutes,
                                payments = sub.BestPayments
                                    .Select(pp => new PaymentDto { id = pp.id, purchaseId = pp.purchaseId, hours = pp.hours, price = pp.price })
                                    .ToList()
                            };
                        }
                    }
                    customer = new ActiveBookingCustomerDto
                    {
                        childId = b.child.id,
                        name = b.child.name,
                        parentId = parent?.id,
                        parentName = parent?.name,
                        parentLastName = parent?.lastName,
                        phone = parent?.phone,
                        status = tier,
                        purchase = pinfo
                    };
                }

                return new ActiveBookingDto
                {
                    id = b.id,
                    table = b.tableId != null ? new BookingTableDto { id = b.table?.id, name = b.table?.name } : null,
                    customer = customer,
                    price = b.price,
                    startTime = b.startTime,
                    endTime = b.endTime,
                    subscriptionStartTime = b.subscriptionStartTime,
                    subscriptionEndTime = b.subscriptionEndTime,
                    status = b.status.ToString(),
                    note = b.note,
                    logs = b.logs
                        .OrderBy(l => l.id)
                        .Select(l => new BookingLogDto { id = l.id, bookingId = l.bookingId, time = l.time, type = l.type.ToString(), userId = l.userId })
                        .ToList()
                };
            }).ToList();
        }
    }
}
