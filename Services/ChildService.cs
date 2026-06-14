using Iroh.Models.DTOs.Child;
using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Iroh.Services
{
    public class ChildService
    {
        private readonly AppDbContext _context;

        public ChildService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<UnifiedSearchResultDto>> SearchUnified(string search)
        {
            var now = DateTime.Now;

            // Npgsql specific approach for date diff in LINQ
            var query = from customer in _context.Customer
                        where !customer.isDeleted && customer.id != 999999
                        
                        let bestPurchase = _context.Purchase
                            .Where(p => p.customerId == customer.id)
                            .OrderByDescending(p => p.startDate <= now && p.endDate >= now)
                            .ThenByDescending(p => ((p.hours + _context.purchasePayments.Where(pp => pp.purchaseId == p.id).Sum(pp => pp.hours)) * 60) - 
                                (_context.purchaseBookings
                                    .Where(pb => pb.purchaseId == p.id)
                                    .Join(_context.Booking, pb => pb.bookingId, b => b.id, (pb, b) => b)
                                    .Sum(b => b.subscriptionEndTime.HasValue && b.subscriptionStartTime.HasValue 
                                        ? (b.subscriptionEndTime.Value - b.subscriptionStartTime.Value).TotalMinutes 
                                        : 0)) > 0)
                            .ThenByDescending(p => p.endDate)
                            .FirstOrDefault()

                        let totalHours = bestPurchase != null ? (bestPurchase.hours + _context.purchasePayments.Where(pp => pp.purchaseId == bestPurchase.id).Sum(pp => pp.hours)) : 0
                        let usedMinutes = bestPurchase != null 
                            ? _context.purchaseBookings
                                .Where(pb => pb.purchaseId == bestPurchase.id)
                                .Join(_context.Booking, pb => pb.bookingId, b => b.id, (pb, b) => b)
                                .Sum(b => b.subscriptionEndTime.HasValue && b.subscriptionStartTime.HasValue 
                                    ? (b.subscriptionEndTime.Value - b.subscriptionStartTime.Value).TotalMinutes 
                                    : 0)
                            : 0
                        
                        let remMinutes = bestPurchase != null ? (totalHours * 60) - usedMinutes : 0
                        let isDateValid = bestPurchase != null && bestPurchase.startDate <= now && bestPurchase.endDate >= now
                        let hasUpcoming = _context.Purchase.Any(p => p.customerId == customer.id && p.startDate > now)
                        let hasAnyPurchase = _context.Purchase.Any(p => p.customerId == customer.id)
                        
                        from child in _context.Children
                            .Where(ch => ch.parentId == customer.id && !ch.isDeleted)
                            .DefaultIfEmpty()
                        
                        where string.IsNullOrEmpty(search) || 
                              customer.name.ToLower().Contains(search.ToLower()) || 
                              (customer.lastName != null && customer.lastName.ToLower().Contains(search.ToLower())) || 
                              (customer.phone != null && customer.phone.Contains(search)) || 
                              (child != null && child.name.ToLower().Contains(search.ToLower()))

                        select new UnifiedSearchResultDto
                        {
                            child_id = child != null ? child.id : 0,
                            child_name = child != null ? child.name : "",
                            parent_id = customer.id,
                            parent_name = customer.name + " " + (customer.lastName ?? ""),
                            parent_phone = customer.phone ?? "",
                            status = isDateValid && remMinutes > 0 ? "ActiveSubscriber" :
                                     isDateValid ? "OverageSubscriber" :
                                     hasUpcoming ? "UpcomingSubscriber" :
                                     hasAnyPurchase ? "Subscriber" : "Customer",
                            remaining_hours = (decimal)(remMinutes / 60.0), 
                            is_active = _context.Booking.Any(b => b.childId == (child != null ? child.id : -1) && (b.status == BookingStatus.Active || b.status == BookingStatus.Paused)),
                            current_table_name = (from b in _context.Booking
                                                 join t in _context.Table on b.tableId equals t.id
                                                 where b.childId == (child != null ? child.id : -1) && (b.status == BookingStatus.Active || b.status == BookingStatus.Paused)
                                                 select t.name).FirstOrDefault()
                        };

            var results = await query
                .OrderBy(r => r.status == "ActiveSubscriber" ? 0 :
                             r.status == "OverageSubscriber" ? 1 :
                             r.status == "UpcomingSubscriber" ? 2 : 3)
                .ThenBy(r => r.parent_name)
                .Take(50)
                .ToListAsync();

            return results;
        }

        public async Task<Child?> CreateChild(long parentId, string name, DateTime? birthDate)
        {
            if (parentId == 999999)
            {
                throw new Exception("Sistem Misafiri kaydına ek çocuk eklenemez!");
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
                throw new Exception("Çocuk bulunamadı veya silinmiş!");
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
            // Aktif oturum var mı kontrol et
            var hasActiveBooking = await _context.Booking.AnyAsync(b => b.childId == id && (b.status == Models.Enums.BookingStatus.Active || b.status == Models.Enums.BookingStatus.Paused));
            if (hasActiveBooking)
            {
                throw new Exception("Bu çocuğun şu an içeride aktif bir oturumu var. Oturum kapatılmadan silinemez!");
            }

            var child = await _context.Children.FindAsync((int)id);
            if (child == null)
            {
                throw new Exception("Çocuk bulunamadı!");
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
