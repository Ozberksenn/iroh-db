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
            return await _context.CustomerPurchaseResults
                .FromSqlInterpolated($"SELECT * FROM fn_get_purchase_by_customer_id({customerId})")
                .ToListAsync();
        }

        public async Task<List<PurchaseBookingResultDto>> GetPurchaseBookings(long purchaseId)
        {
            return await _context.PurchaseBookingResults
                .FromSqlInterpolated($"SELECT * FROM usp_get_purchase_bookings_by_id({purchaseId})")
                .ToListAsync();
        }

        public async Task Create(Purchase purchase)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($"CALL usp_insert_purchase({purchase.hours}, {purchase.price}, {purchase.customerId}, {purchase.startDate}, {purchase.endDate})");
        }

        public async Task Update(long id, int hours, int price, long customerId, DateTime? startDate, DateTime? endDate)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($"CALL usp_update_purchase({id}, {hours}, {price}, {customerId}, {startDate}, {endDate})");
        }

        public async Task Delete(long id)
        {
            // Bu prosedür her zaman hata fırlatacak (iş kuralı gereği)
            await _context.Database.ExecuteSqlInterpolatedAsync($"CALL usp_delete_purchase({id})");
        }
    }
}
