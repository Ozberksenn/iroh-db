using Iroh.Models.Entities;

namespace Iroh.Services
{
    public class CustomerService
    {
        private readonly AppDbContext _context;
        public CustomerService(AppDbContext context)
        {
            _context = context;
        }
        public List<Customer> GetAll()
        {
            return _context.Customer.ToList();
        }
        // public Table? GetById(int id)
        // {
        //     // _context.Table -> Senin DbSet'in (tablon)
        //     // .FirstOrDefault -> Şarta uyan İLK kaydı getir, bulamazsan 'null' dön.
        //     // t => t.id == id -> Lambda ifadesi (Sorgu şartı)
        //     return _context.Table.FirstOrDefault(t => t.id == id);
        // }


    }
}