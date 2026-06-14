using Iroh.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public interface IPurchasePaymentService
    {
        Task<List<PurchasePayment>> GetAll();
        Task<PurchasePayment> Create(PurchasePayment purchasePayment);
    }

    public class PurchasePaymentService : IPurchasePaymentService
    {
        private readonly AppDbContext _context;
        public PurchasePaymentService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<PurchasePayment>> GetAll() =>
            await _context.PurchasePayments.AsNoTracking().ToListAsync();

        public async Task<PurchasePayment> Create(PurchasePayment purchasePayment)
        {
            _context.PurchasePayments.Add(purchasePayment);
            await _context.SaveChangesAsync();
            return purchasePayment;
        }
    }
}
