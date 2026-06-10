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
            return _context.Customer.Where(c => !c.isDeleted && c.id != 999999).ToList();
        }

        public async Task<(List<object> items, double totalCount)> GetPaginated(string? status, int page, int size, string? name)
        {
            var now = DateTime.Now;

            var baseQuery = from c in _context.Customer
                            where !c.isDeleted && c.id != 999999
                            let customerStatus = _context.Purchase.Any(p => p.customerId == c.id && p.startDate <= now && p.endDate >= now) ? "ActiveSubscriber" :
                                                _context.Purchase.Any(p => p.customerId == c.id) ? "Subscriber" : "Customer"
                            let childrenNames = _context.Children
                                                .Where(ch => ch.parentId == c.id && !ch.isDeleted)
                                                .Select(ch => ch.name)
                            select new
                            {
                                id = c.id,
                                name = c.name,
                                lastName = c.lastName,
                                phone = c.phone,
                                mail = c.mail,
                                status = customerStatus,
                                childrenNamesString = string.Join(" ", childrenNames)
                            };

            var filteredQuery = baseQuery.Where(c => 
                (string.IsNullOrEmpty(name) || (
                    c.name + " " + (c.lastName ?? "") + " " + (c.phone ?? "") + " " + (c.mail ?? "") + " " + c.childrenNamesString
                ).ToLower().Contains(name.ToLower())) &&
                (string.IsNullOrEmpty(status) || c.status == status)
            );

            var filteredList = filteredQuery.ToList();
            var totalCount = filteredList.Count();
            
            var results = filteredList
                .OrderBy(c => c.id)
                .Skip(page == -1 ? 0 : (page - 1) * size)
                .Take(page == -1 ? int.MaxValue : size)
                .ToList();

            return (results.Cast<object>().ToList(), (double)totalCount);
        }
        public Customer Create(Customer customer)
        {
            _context.Customer.Add(customer);
            _context.SaveChanges();
            return customer;
        }

        public Customer Update(Customer customer)
        {
            if (customer.id == 999999)
            {
                throw new InvalidOperationException("Sistem Misafiri kaydı değiştirilemez!");
            }

            customer.updatedAt = DateTime.UtcNow;
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