using Iroh.Models.Entities;

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

        public Purchase Create(Purchase purchaseCreateDto)
        {
            if (purchaseCreateDto.customerId == 999999)
            {
                throw new InvalidOperationException("Sistem Misafiri kaydına paket tanımlanamaz!");
            }
            _context.Purchase.Add(purchaseCreateDto);
            _context.SaveChanges();
            return purchaseCreateDto;
        }

        public Purchase Update(Purchase purchase, int newHours, int newPrice, int newCustomerId, DateTime? newStartDate, DateTime? newEndDate)
        {
            // 1. Mevcut saati ve kullanım durumlarını kontrol et
            // Not: purchase nesnesi zaten veritabanından çekilmiş eski değerleri taşıyor olmalı.

            bool hasUsage = _context.purchaseBookings.Any(pb => pb.purchaseId == purchase.id);
            bool hasPayments = _context.purchasePayments.Any(pp => pp.purchaseId == purchase.id);

            // 2. Eğer saat değiştirilmek isteniyorsa ve kullanım/ödeme varsa ENGELLE
            if (purchase.hours != newHours && (hasUsage || hasPayments))
            {
                throw new InvalidOperationException("Bu paket üzerinde kullanım veya ek ödeme mevcut. Ana saat bilgisi değiştirilemez! Lütfen düzeltme için ek ödeme (payment) ekleyin.");
            }

            // 3. Güncelleme işlemini yap
            purchase.hours = newHours;
            purchase.price = newPrice;
            purchase.customerId = newCustomerId;
            purchase.startDate = newStartDate;
            purchase.endDate = newEndDate;
            purchase.updatedAt = DateTime.UtcNow;

            _context.Purchase.Update(purchase);
            _context.SaveChanges();
            return purchase;
        }

    }
}