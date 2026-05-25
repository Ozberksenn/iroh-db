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

        public Customer? GetCustomerById(int id)
        {
            return _context.Customer.FirstOrDefault(c => c.id == id);
        }

        public List<Customer> GetAll()
        {
            return _context.Customer.ToList();
        }
        public Customer Create(Customer customer)
        {
            _context.Customer.Add(customer);
            _context.SaveChanges();
            return customer;
        }

        public Customer Update(Customer customer)
        {
            _context.Customer.Update(customer);
            _context.SaveChanges();
            return customer;
        }

    }
}