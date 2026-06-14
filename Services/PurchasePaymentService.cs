using Iroh.Models.Entities;

namespace Iroh.Services
{
    public class PurchasePaymentService
    {
        private readonly AppDbContext _context;
        public PurchasePaymentService(AppDbContext context)
        {
            _context = context;
        }
        public List<PurchasePayment> GetAll()
        {
            return _context.purchasePayments.ToList();
        }

        public PurchasePayment Create(PurchasePayment purchasePayment)
        {
            _context.purchasePayments.Add(purchasePayment);
            _context.SaveChanges();
            return purchasePayment;
        }

    }
}