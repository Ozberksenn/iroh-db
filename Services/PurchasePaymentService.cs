using Iroh.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public class PurchasePaymentService
    {
        private readonly AppDbContext _context;
        public PurchasePaymentService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<PurchasePayment>> GetAll() =>
            await _context.purchasePayments.ToListAsync();

        public async Task<PurchasePayment> Create(PurchasePayment purchasePayment)
        {
            _context.purchasePayments.Add(purchasePayment);
            await _context.SaveChangesAsync();
            return purchasePayment;
        }
    }
}
