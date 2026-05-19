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

    }
}