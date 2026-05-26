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

        public Purchase GetById(int id)
        {
            var purchase = _context.Purchase.Find(id);
            if (purchase == null)
            {
                throw new KeyNotFoundException("Purchase not found");
            }
            return purchase;
        }

        public PurchasePayment Create(PurchasePayment purchasePayment)
        {
            _context.purchasePayments.Add(purchasePayment);
            _context.SaveChanges();
            return purchasePayment;
        }

    }
}