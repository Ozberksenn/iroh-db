using Iroh.Models.Entities;
using Iroh.Models.DTOs.Purchase;
using Iroh.Models.DTOs.Booking;
using Iroh.Domain;
using Iroh.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public interface IPurchaseService
    {
        Task<List<PurchaseListDto>> GetAll();
        Purchase GetById(int id);
        Task<List<CustomerPurchaseResultDto>> GetByCustomerId(int customerId);
        Task<List<PurchaseBookingResultDto>> GetPurchaseBookings(int purchaseId);
        Task Create(Purchase purchase);
        Task Update(PurchaseUpdateDto dto);
        Task Delete(int id);
    }

    public class PurchaseService : IPurchaseService
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
                    Id = p.Id,
                    Hours = p.Hours,
                    Price = p.Price,
                    CustomerId = p.CustomerId,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate
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
                .Where(p => p.CustomerId == customerId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            if (purchases.Count == 0) return new();

            var purchaseIds = purchases.Select(p => p.Id).ToList();
            var payments = await _context.PurchasePayments
                .AsNoTracking()
                .Where(pp => purchaseIds.Contains(pp.PurchaseId))
                .ToListAsync();
            var linked = await _context.PurchaseBookings
                .Where(pb => purchaseIds.Contains(pb.PurchaseId))
                .Join(_context.Bookings, pb => pb.BookingId, b => b.Id,
                      (pb, b) => new { pb.PurchaseId, b.SubscriptionStartTime, b.SubscriptionEndTime })
                .ToListAsync();

            // fn_get_used_hours: Σ(subEnd - subStart) dakika (eski .NET /60 yapıp yanlış HOURS dönüyordu).
            double UsedMinutesFor(int pid) => linked
                .Where(x => x.PurchaseId == pid && x.SubscriptionStartTime.HasValue && x.SubscriptionEndTime.HasValue)
                .Sum(x => (x.SubscriptionEndTime!.Value - x.SubscriptionStartTime!.Value).TotalMinutes);

            return purchases.Select(p => new CustomerPurchaseResultDto
            {
                Id = p.Id,
                CustomerId = p.CustomerId,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                Hours = (double)p.Hours,
                Price = (double)p.Price,
                UsedMinutes = UsedMinutesFor(p.Id),
                Payments = payments.Where(pp => pp.PurchaseId == p.Id)
                    .Select(pp => new PaymentDto { Id = pp.Id, PurchaseId = pp.PurchaseId, Hours = pp.Hours, Price = pp.Price })
                    .ToList()
            }).ToList();
        }

        // usp_get_purchase_bookings_by_id: pakete bağlı bookings (nested booking). Proc INNER JOIN tables → sadece masası olanlar.
        // Proc'taki bozuk b.CustomerId (yok olan kolon) yerine customerId child.ParentId'den türetilir (D7).
        public async Task<List<PurchaseBookingResultDto>> GetPurchaseBookings(int purchaseId)
        {
            var rows = await _context.PurchaseBookings
                .Where(pb => pb.PurchaseId == purchaseId && pb.Booking != null && pb.Booking.TableId != null)
                .Select(pb => new
                {
                    pb.Id,
                    pb.PurchaseId,
                    pb.BookingId,
                    tableId = pb.Booking!.TableId,
                    tableName = pb.Booking.Table != null ? pb.Booking.Table.Name : null,
                    customerId = pb.Booking.Child != null ? (int?)pb.Booking.Child.ParentId : null,
                    startTime = pb.Booking.StartTime,
                    endTime = pb.Booking.EndTime,
                    status = pb.Booking.Status,
                    note = pb.Booking.Note
                })
                .ToListAsync();

            return rows.Select(r => new PurchaseBookingResultDto
            {
                Id = r.Id,
                purchase_id = r.PurchaseId,
                booking_id = r.BookingId,
                Booking = new BookingBriefDto
                {
                    Table = r.tableId != null ? new BookingTableDto { Id = r.tableId, Name = r.tableName } : null,
                    TableId = r.tableId,
                    CustomerId = r.customerId,
                    StartTime = r.startTime,
                    EndTime = r.endTime,
                    Status = r.status.ToString(),
                    Note = r.note
                }
            }).ToList();
        }

        public async Task Create(Purchase purchase)
        {
            if (purchase.CustomerId == SystemConstants.GuestCustomerId)
            {
                throw new BusinessRuleException("Sistem Misafiri kaydına paket tanımlanamaz!");
            }
            purchase.CreatedAt = DateTime.Now;
            _context.Purchases.Add(purchase);
            await _context.SaveChangesAsync();
        }

        // usp_update_purchase: kullanım/ödeme varsa ana saat değiştirilemez.
        public async Task Update(PurchaseUpdateDto dto)
        {
            var purchase = await _context.Purchases.FindAsync(dto.Id);
            if (purchase == null)
            {
                throw new NotFoundException("Paket bulunamadı!");
            }

            var hasUsage = await _context.PurchaseBookings.AnyAsync(pb => pb.PurchaseId == dto.Id);
            var hasPayments = await _context.PurchasePayments.AnyAsync(pp => pp.PurchaseId == dto.Id);

            if (purchase.Hours != dto.Hours && (hasUsage || hasPayments))
            {
                throw new BusinessRuleException("Bu paket üzerinde kullanım veya ek ödeme mevcut. Ana saat bilgisi değiştirilemez! Lütfen düzeltme için ek ödeme (payment) ekleyin.");
            }

            purchase.Hours = dto.Hours;
            purchase.Price = dto.Price;
            purchase.CustomerId = dto.CustomerId;
            purchase.StartDate = dto.StartDate;
            purchase.EndDate = dto.EndDate;
            purchase.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
        }

        public async Task Delete(int id)
        {
            // Bu işlem güvenlik gereği engellenmiştir
            throw new BusinessRuleException("Güvenlik ve denetim gereği satın alım kayıtları silinemez! Yanlış bir işlem yaptıysanız lütfen ek ödeme (payment) ile bakiyeyi dengeleyin veya yöneticiye başvurun.");
        }
    }
}
