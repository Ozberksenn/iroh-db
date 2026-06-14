using Iroh.Models.Entities;
using Iroh.Models.DTOs.Purchase;
using Iroh.Models.DTOs.Booking;
using Iroh.Domain;
using Iroh.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public class PurchaseService
    {
        private readonly AppDbContext _context;
        public PurchaseService(AppDbContext context)
        {
            _context = context;
        }

        // vw_purchases: id, hours, price, customerId, startDate, endDate
        public async Task<List<PurchaseListDto>> GetAll()
        {
            return await _context.Purchases
                .Select(p => new PurchaseListDto
                {
                    id = p.id,
                    hours = p.hours,
                    price = p.price,
                    customerId = p.customerId,
                    startDate = p.startDate,
                    endDate = p.endDate
                })
                .ToListAsync();
        }

        public Purchase GetById(int id)
        {
            var purchase = _context.Purchases.Find(id);
            if (purchase == null)
            {
                throw new KeyNotFoundException("Purchase not found");
            }
            return purchase;
        }

        // fn_get_purchase_by_customer_id: müşterinin paketleri + usedMinutes (DAKİKA) + gerçek payments listesi.
        public async Task<List<CustomerPurchaseResultDto>> GetByCustomerId(int customerId)
        {
            var purchases = await _context.Purchases
                .AsNoTracking()
                .Where(p => p.customerId == customerId)
                .OrderByDescending(p => p.createdAt)
                .ToListAsync();
            if (purchases.Count == 0) return new();

            var purchaseIds = purchases.Select(p => p.id).ToList();
            var payments = await _context.PurchasePayments
                .AsNoTracking()
                .Where(pp => purchaseIds.Contains(pp.purchaseId))
                .ToListAsync();
            var linked = await _context.PurchaseBookings
                .Where(pb => purchaseIds.Contains(pb.purchaseId))
                .Join(_context.Bookings, pb => pb.bookingId, b => b.id,
                      (pb, b) => new { pb.purchaseId, b.subscriptionStartTime, b.subscriptionEndTime })
                .ToListAsync();

            // fn_get_used_hours: Σ(subEnd - subStart) dakika (eski .NET /60 yapıp yanlış HOURS dönüyordu).
            double UsedMinutesFor(int pid) => linked
                .Where(x => x.purchaseId == pid && x.subscriptionStartTime.HasValue && x.subscriptionEndTime.HasValue)
                .Sum(x => (x.subscriptionEndTime!.Value - x.subscriptionStartTime!.Value).TotalMinutes);

            return purchases.Select(p => new CustomerPurchaseResultDto
            {
                id = p.id,
                customerId = p.customerId,
                startDate = p.startDate,
                endDate = p.endDate,
                hours = (double)p.hours,
                price = (double)p.price,
                usedMinutes = UsedMinutesFor(p.id),
                payments = payments.Where(pp => pp.purchaseId == p.id)
                    .Select(pp => new PaymentDto { id = pp.id, purchaseId = pp.purchaseId, hours = pp.hours, price = pp.price })
                    .ToList()
            }).ToList();
        }

        // usp_get_purchase_bookings_by_id: pakete bağlı bookings (nested booking). Proc INNER JOIN tables → sadece masası olanlar.
        // Proc'taki bozuk b.customerId (yok olan kolon) yerine customerId child.parentId'den türetilir (D7).
        public async Task<List<PurchaseBookingResultDto>> GetPurchaseBookings(int purchaseId)
        {
            var rows = await _context.PurchaseBookings
                .Where(pb => pb.purchaseId == purchaseId && pb.booking != null && pb.booking.tableId != null)
                .Select(pb => new
                {
                    pb.id,
                    pb.purchaseId,
                    pb.bookingId,
                    tableId = pb.booking!.tableId,
                    tableName = pb.booking.table != null ? pb.booking.table.name : null,
                    customerId = pb.booking.child != null ? (int?)pb.booking.child.parentId : null,
                    startTime = pb.booking.startTime,
                    endTime = pb.booking.endTime,
                    status = pb.booking.status,
                    note = pb.booking.note
                })
                .ToListAsync();

            return rows.Select(r => new PurchaseBookingResultDto
            {
                id = r.id,
                purchase_id = r.purchaseId,
                booking_id = r.bookingId,
                booking = new BookingBriefDto
                {
                    table = r.tableId != null ? new BookingTableDto { id = r.tableId, name = r.tableName } : null,
                    tableId = r.tableId,
                    customerId = r.customerId,
                    startTime = r.startTime,
                    endTime = r.endTime,
                    status = r.status.ToString(),
                    note = r.note
                }
            }).ToList();
        }

        public async Task Create(Purchase purchase)
        {
            if (purchase.customerId == SystemConstants.GuestCustomerId)
            {
                throw new BusinessRuleException("Sistem Misafiri kaydına paket tanımlanamaz!");
            }
            purchase.createdAt = DateTime.Now;
            _context.Purchases.Add(purchase);
            await _context.SaveChangesAsync();
        }

        // usp_update_purchase: kullanım/ödeme varsa ana saat değiştirilemez.
        public async Task Update(PurchaseUpdateDto dto)
        {
            var purchase = await _context.Purchases.FindAsync(dto.id);
            if (purchase == null)
            {
                throw new NotFoundException("Paket bulunamadı!");
            }

            var hasUsage = await _context.PurchaseBookings.AnyAsync(pb => pb.purchaseId == dto.id);
            var hasPayments = await _context.PurchasePayments.AnyAsync(pp => pp.purchaseId == dto.id);

            if (purchase.hours != dto.hours && (hasUsage || hasPayments))
            {
                throw new BusinessRuleException("Bu paket üzerinde kullanım veya ek ödeme mevcut. Ana saat bilgisi değiştirilemez! Lütfen düzeltme için ek ödeme (payment) ekleyin.");
            }

            purchase.hours = dto.hours;
            purchase.price = dto.price;
            purchase.customerId = dto.customerId;
            purchase.startDate = dto.startDate;
            purchase.endDate = dto.endDate;
            purchase.updatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
        }

        public async Task Delete(int id)
        {
            // Bu işlem güvenlik gereği engellenmiştir
            throw new BusinessRuleException("Güvenlik ve denetim gereği satın alım kayıtları silinemez! Yanlış bir işlem yaptıysanız lütfen ek ödeme (payment) ile bakiyeyi dengeleyin veya yöneticiye başvurun.");
        }
    }
}
