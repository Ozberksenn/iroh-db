using Iroh.Models.DTOs.Child;
using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Iroh.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public interface IChildService
    {
        Task<List<UnifiedSearchResultDto>> SearchUnified(string search);
        Task<Child?> CreateChild(int parentId, string name, DateTime? birthDate);
        Task<List<Child>> GetChildrenByParentId(int parentId);
        Task UpdateChild(int id, string name, DateTime? birthDate);
        Task DeleteChild(int id);
        Task<Child?> GetById(int id);
    }

    public class ChildService : IChildService
    {
        private const int SystemGuestId = Iroh.Domain.SystemConstants.GuestCustomerId;

        private readonly AppDbContext _context;
        private readonly ISubscriptionService _subscriptionService;

        public ChildService(AppDbContext context, ISubscriptionService subscriptionService)
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
                from c in _context.Customers.Where(c => !c.IsDeleted && c.Id != SystemGuestId)
                from ch in _context.Children.Where(ch => ch.ParentId == c.Id && !ch.IsDeleted).DefaultIfEmpty()
                where EF.Functions.ILike(c.Name, pattern)
                   || (c.LastName != null && EF.Functions.ILike(c.LastName, pattern))
                   || (c.Phone != null && EF.Functions.ILike(c.Phone, pattern))
                   || (ch != null && EF.Functions.ILike(ch.Name, pattern))
                select new
                {
                    parentId = c.Id,
                    parentName = c.Name,
                    parentLastName = c.LastName,
                    parentPhone = c.Phone,
                    childId = ch != null ? (int?)ch.Id : null,
                    childName = ch != null ? ch.Name : null
                }
            ).ToListAsync();

            if (rows.Count == 0) return new List<UnifiedSearchResultDto>();

            var parentIds = rows.Select(r => r.parentId).Distinct().ToList();
            var subs = await _subscriptionService.ComputeForParents(parentIds);

            // is_active + current_table_name: Active/Paused oturumu olan çocuklar.
            var childIds = rows.Where(r => r.childId.HasValue).Select(r => r.childId!.Value).Distinct().ToList();
            var activeBookings = await _context.Bookings
                .Where(b => b.ChildId != null && childIds.Contains(b.ChildId.Value)
                         && (b.Status == BookingStatus.Active || b.Status == BookingStatus.Paused))
                .Select(b => new { ChildId = b.ChildId!.Value, TableName = b.Table != null ? b.Table.Name : null })
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

                // Tek statü fonksiyonu (docs/wallet-redesign.md §3) — active-bookings ile ortak.
                var status = WalletService.Derive(sub.BestIsDateValid, sub.BestRemainingMinutes > 0, sub.HasUpcoming, sub.HasAny).ToString();

                return new
                {
                    dto = new UnifiedSearchResultDto
                    {
                        child_id = r.childId ?? 0,
                        child_name = r.childName ?? "",
                        parent_id = r.parentId,
                        parent_name = r.parentName + " " + (r.parentLastName ?? ""),
                        parent_phone = r.parentPhone ?? "",
                        Status = status,
                        // Net pozisyon: kullanılabilir bakiye − süre-borcu. Borçluysa NEGATİF döner
                        // (bakiye 0 + 60dk borç → -1sa); picker bunu amber "aşımda" görseliyle gösterir.
                        remaining_hours = (decimal)((sub.BestRemainingMinutes - sub.TimeDebtMinutes) / 60.0),
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

        public async Task<Child?> CreateChild(int parentId, string name, DateTime? birthDate)
        {
            if (parentId == SystemGuestId)
            {
                throw new BusinessRuleException("Sistem Misafiri kaydına ek çocuk eklenemez!");
            }

            var child = new Child
            {
                ParentId = parentId,
                Name = name,
                BirthDate = birthDate,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Children.Add(child);
            await _context.SaveChangesAsync();
            return child;
        }

        public async Task<List<Child>> GetChildrenByParentId(int parentId)
        {
            return await _context.Children
                .Where(c => c.ParentId == parentId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task UpdateChild(int id, string name, DateTime? birthDate)
        {
            var child = await _context.Children.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
            if (child == null)
            {
                throw new NotFoundException("Çocuk bulunamadı veya silinmiş!");
            }

            child.Name = name;
            if (birthDate.HasValue)
            {
                child.BirthDate = birthDate.Value;
            }
            child.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteChild(int id)
        {
            var hasActiveBooking = await _context.Bookings.AnyAsync(b => b.ChildId == id && (b.Status == BookingStatus.Active || b.Status == BookingStatus.Paused));
            if (hasActiveBooking)
            {
                throw new BusinessRuleException("Bu çocuğun şu an içeride aktif bir oturumu var. Oturum kapatılmadan silinemez!");
            }

            var child = await _context.Children.FindAsync(id);
            if (child == null)
            {
                throw new NotFoundException("Çocuk bulunamadı!");
            }

            child.IsDeleted = true;
            child.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<Child?> GetById(int id)
        {
            return await _context.Children.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        }
    }
}
