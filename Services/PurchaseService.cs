using Iroh.Models.Entities;
using Iroh.Models.DTOs.Purchase;
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

        public List<Purchase> GetAll()
        {
            // vw_purchases view'ını kullanabiliriz ama entity eşleşmesi için doğrudan tabloyu da çekebiliriz
            return _context.Purchase.ToList();
        }

        public Purchase GetById(int id)
        {
            var purchase = _context.Purchase.Find(id);
            if (purchase == null)
            {
                throw new KeyNotFoundException("Purchase not found");
            }
            return purchase;
        }

        public async Task<List<CustomerPurchaseResultDto>> GetByCustomerId(long customerId)
        {
            var purchases = await _context.Purchase
                .Where(p => p.customerId == customerId)
                .OrderByDescending(p => p.createdAt)
                .Select(p => new CustomerPurchaseResultDto
                {
                    id = p.id,
                    customerId = p.customerId,
                    startDate = p.startDate,
                    endDate = p.endDate,
                    hours = p.hours,
                    price = p.price,
                    usedHours = _context.purchaseBookings
                        .Where(pb => pb.purchaseId == p.id)
                        .Join(_context.Booking, pb => pb.bookingId, b => b.id, (pb, b) => b)
                        .Sum(b => b.subscriptionEndTime.HasValue && b.subscriptionStartTime.HasValue 
                            ? (b.subscriptionEndTime.Value - b.subscriptionStartTime.Value).TotalMinutes / 60.0 
                            : 0.0),
                    payments = "[]" // TODO: Implement payments serialization if needed
                })
                .ToListAsync();

            return purchases;
        }

        public async Task<List<PurchaseBookingResultDto>> GetPurchaseBookings(long purchaseId)
        {
            return await _context.purchaseBookings
                .Where(pb => pb.purchaseId == purchaseId)
                .Select(pb => new PurchaseBookingResultDto
                {
                    id = pb.id,
                    purchase_id = pb.purchaseId,
                    booking_id = pb.bookingId,
                    booking = "{}" // TODO: Implement booking serialization if needed
                })
                .ToListAsync();
        }

        public async Task Create(Purchase purchase)
        {
            if (purchase.customerId == 999999)
            {
                throw new Exception("Sistem Misafiri kaydına paket tanımlanamaz!");
            }
            purchase.createdAt = DateTime.Now;
            _context.Purchase.Add(purchase);
            await _context.SaveChangesAsync();
        }

        public async Task Update(long id, int hours, int price, long customerId, DateTime? startDate, DateTime? endDate)
        {
            var purchase = await _context.Purchase.FindAsync((int)id);
            if (purchase == null)
            {
                throw new Exception("Paket bulunamadı!");
            }

            // usp_update_purchase logic: check usage and payments
            var hasUsage = await _context.purchaseBookings.AnyAsync(pb => pb.purchaseId == id);
            var hasPayments = await _context.purchasePayments.AnyAsync(pp => pp.purchaseId == (int)id);

            if (purchase.hours != hours && (hasUsage || hasPayments))
            {
                throw new Exception("Bu paket üzerinde kullanım veya ek ödeme mevcut. Ana saat bilgisi değiştirilemez! Lütfen düzeltme için ek ödeme (payment) ekleyin.");
            }

            purchase.hours = hours;
            purchase.price = price;
            purchase.customerId = (int)customerId;
            purchase.startDate = startDate;
            purchase.endDate = endDate;
            purchase.updatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
        }

        public async Task Delete(long id)
        {
            // Bu işlem güvenlik gereği engellenmiştir
            throw new Exception("Güvenlik ve denetim gereği satın alım kayıtları silinemez! Yanlış bir işlem yaptıysanız lütfen ek ödeme (payment) ile bakiyeyi dengeleyin veya yöneticiye başvurun.");
        }
    }
}
