using Iroh.Models.DTOs.Child;
using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Iroh.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public class ChildService
    {
        private const int SystemGuestId = 999999;

        private readonly AppDbContext _context;
        private readonly SubscriptionService _subscriptionService;

        public ChildService(AppDbContext context, SubscriptionService subscriptionService)
        {
            _context = context;
            _subscriptionService = subscriptionService;
        }

        // fn_search_unified: customers LEFT JOIN children + arama; ebeveyn başına en iyi paket → 4 kademeli statü
        // (DİKKAT: proc'ta "Subscriber" kademesi YOK; eski LINQ'teki 5. kademe yanlıştı, düzeltildi). billing çekirdeği reuse.
        public async Task<List<UnifiedSearchResultDto>> SearchUnified(string search)
        {
            var pattern = "%" + (search ?? "") + "%";

            var rows = await (
                from c in _context.Customer.Where(c => !c.isDeleted && c.id != SystemGuestId)
                from ch in _context.Children.Where(ch => ch.parentId == c.id && !ch.isDeleted).DefaultIfEmpty()
                where EF.Functions.ILike(c.name, pattern)
                   || (c.lastName != null && EF.Functions.ILike(c.lastName, pattern))
                   || (c.phone != null && EF.Functions.ILike(c.phone, pattern))
                   || (ch != null && EF.Functions.ILike(ch.name, pattern))
                select new
                {
                    parentId = c.id,
                    parentName = c.name,
                    parentLastName = c.lastName,
                    parentPhone = c.phone,
                    childId = ch != null ? (int?)ch.id : null,
                    childName = ch != null ? ch.name : null
                }
            ).ToListAsync();

            if (rows.Count == 0) return new List<UnifiedSearchResultDto>();

            var parentIds = rows.Select(r => r.parentId).Distinct().ToList();
            var subs = await _subscriptionService.ComputeForParents(parentIds);

            // is_active + current_table_name: Active/Paused oturumu olan çocuklar.
            var childIds = rows.Where(r => r.childId.HasValue).Select(r => r.childId!.Value).Distinct().ToList();
            var activeBookings = await _context.Booking
                .Where(b => b.childId != null && childIds.Contains(b.childId.Value)
                         && (b.status == BookingStatus.Active || b.status == BookingStatus.Paused))
                .Select(b => new { ChildId = b.childId!.Value, TableName = b.table != null ? b.table.name : null })
                .ToListAsync();
            var activeChildIds = activeBookings.Select(x => x.ChildId).ToHashSet();
            var tableByChild = activeBookings
                .Where(x => x.TableName != null)
                .GroupBy(x => x.ChildId)
                .ToDictionary(g => g.Key, g => g.First().TableName);

            return rows.Select(r =>
            {
                subs.TryGetValue(r.parentId, out var sub);
                sub ??= new SubscriptionService.ParentSubscription();

                // 4 kademe (Subscriber YOK):
                var status = (sub.BestIsDateValid && sub.BestRemainingMinutes > 0) ? "ActiveSubscriber"
                           : sub.BestIsDateValid ? "OverageSubscriber"
                           : sub.HasUpcoming ? "UpcomingSubscriber"
                           : "Customer";

                return new
                {
                    dto = new UnifiedSearchResultDto
                    {
                        child_id = r.childId ?? 0,
                        child_name = r.childName ?? "",
                        parent_id = r.parentId,
                        parent_name = r.parentName + " " + (r.parentLastName ?? ""),
                        parent_phone = r.parentPhone ?? "",
                        status = status,
                        remaining_hours = (decimal)(sub.BestRemainingMinutes / 60.0),
                        is_active = r.childId.HasValue && activeChildIds.Contains(r.childId.Value),
                        current_table_name = r.childId.HasValue && tableByChild.TryGetValue(r.childId.Value, out var tn) ? tn : null
                    },
                    orderKey = (sub.BestIsDateValid && sub.BestRemainingMinutes > 0) ? 0 : sub.BestIsDateValid ? 1 : 2,
                    sortName = r.parentName
                };
            })
            .OrderBy(x => x.orderKey)
            .ThenBy(x => x.sortName)
            .Take(50)
            .Select(x => x.dto)
            .ToList();
        }

        public async Task<Child?> CreateChild(long parentId, string name, DateTime? birthDate)
        {
            if (parentId == SystemGuestId)
            {
                throw new BusinessRuleException("Sistem Misafiri kaydına ek çocuk eklenemez!");
            }

            var child = new Child
            {
                parentId = (int)parentId,
                name = name,
                birthDate = birthDate ?? DateTime.MinValue,
                isDeleted = false,
                createdAt = DateTime.Now,
                updatedAt = DateTime.Now
            };

            _context.Children.Add(child);
            await _context.SaveChangesAsync();
            return child;
        }

        public async Task<List<Child>> GetChildrenByParentId(long parentId)
        {
            return await _context.Children
                .Where(c => c.parentId == parentId && !c.isDeleted)
                .OrderByDescending(c => c.createdAt)
                .ToListAsync();
        }

        public async Task UpdateChild(long id, string name, DateTime? birthDate)
        {
            var child = await _context.Children.FirstOrDefaultAsync(c => c.id == id && !c.isDeleted);
            if (child == null)
            {
                throw new NotFoundException("Çocuk bulunamadı veya silinmiş!");
            }

            child.name = name;
            if (birthDate.HasValue)
            {
                child.birthDate = birthDate.Value;
            }
            child.updatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteChild(long id)
        {
            var hasActiveBooking = await _context.Booking.AnyAsync(b => b.childId == id && (b.status == BookingStatus.Active || b.status == BookingStatus.Paused));
            if (hasActiveBooking)
            {
                throw new BusinessRuleException("Bu çocuğun şu an içeride aktif bir oturumu var. Oturum kapatılmadan silinemez!");
            }

            var child = await _context.Children.FindAsync((int)id);
            if (child == null)
            {
                throw new NotFoundException("Çocuk bulunamadı!");
            }

            child.isDeleted = true;
            child.updatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        public async Task<Child?> GetById(long id)
        {
            return await _context.Children.FirstOrDefaultAsync(c => c.id == id && !c.isDeleted);
        }
    }
}
