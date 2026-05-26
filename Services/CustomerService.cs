using Iroh.Models.Entities;
using Iroh.Models.Enums;

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
        public Customer Delete(Customer customer)
        {
            if (customer.id == 999999)
            {
                throw new InvalidOperationException("Sistem Misafiri kaydı silinemez!");
            }

            // Aktif veya Beklemede olan bir oturum (booking) var mı kontrol et
            var hasActiveBooking = _context.Booking
                .Any(b => _context.Children.Any(ch => ch.parentId == customer.id && ch.id == b.childId)
                          && (b.status == BookingStatus.Active || b.status == BookingStatus.Paused));

            if (hasActiveBooking)
            {
                throw new InvalidOperationException("Bu ebeveynin bir çocuğu şu an içeride aktif oturumda. Oturum kapanmadan silinemez!");
            }

            // Çocukları soft-delete yap
            var children = _context.Children.Where(ch => ch.parentId == customer.id && !ch.isDeleted).ToList();
            foreach (var child in children)
            {
                child.isDeleted = true;
                child.updatedAt = DateTime.UtcNow;
            }

            // Müşteriyi soft-delete yap
            customer.isDeleted = true;
            customer.updatedAt = DateTime.UtcNow;

            _context.Customer.Update(customer);
            _context.SaveChanges();
            return customer;
        }

    }
}