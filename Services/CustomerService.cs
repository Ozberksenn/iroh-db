using Iroh.Models.DTOs.Common;
using Iroh.Models.DTOs.Customer;
using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Iroh.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public interface ICustomerService
    {
        Task<Customer?> GetById(int id);
        Task<Customer?> GetCustomerById(int id);
        Task<PagedResult<CustomerListItemDto>> GetCustomers(string? status, int page, int size, string? name);
        Task<Customer> Create(Customer customer);
        Task<Customer> Update(CustomerUpdateDto dto);
        Task<Customer> Delete(int id);
    }

    public class CustomerService : ICustomerService
    {
        private const int SystemGuestId = Iroh.Domain.SystemConstants.GuestCustomerId;

        private readonly AppDbContext _context;
        public CustomerService(AppDbContext context)
        {
            _context = context;
        }

        // fn_get_customer_by_id: yalnızca silinmemiş kayıt döner.
        public async Task<Customer?> GetById(int id) =>
            await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.id == id && !c.isDeleted);

        // Update/Delete'in iç kullanımı — soft-delete filtresi uygulamaz.
        public async Task<Customer?> GetCustomerById(int id) =>
            await _context.Customers.FirstOrDefaultAsync(c => c.id == id);

        // fn_get_customers paritesi: hesaplanmış abone statüsü + serbest metin arama + sayfalama.
        public async Task<PagedResult<CustomerListItemDto>> GetCustomers(string? status, int page, int size, string? name)
        {
            var now = DateTime.UtcNow;

            var query = _context.Customers.Where(c => !c.isDeleted && c.id != SystemGuestId);

            if (!string.IsNullOrWhiteSpace(name))
            {
                var pattern = $"%{name}%";
                query = query.Where(c =>
                    EF.Functions.ILike(
                        c.name + " " + (c.lastName ?? "") + " " + (c.phone ?? "") + " " + (c.mail ?? ""),
                        pattern)
                    || _context.Children.Any(ch => ch.parentId == c.id && EF.Functions.ILike(ch.name, pattern)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = status switch
                {
                    "ActiveSubscriber" => query.Where(c =>
                        _context.Purchases.Any(p => p.customerId == c.id && p.startDate <= now && p.endDate >= now)),
                    "Subscriber" => query.Where(c =>
                        _context.Purchases.Any(p => p.customerId == c.id)
                        && !_context.Purchases.Any(p => p.customerId == c.id && p.startDate <= now && p.endDate >= now)),
                    "Customer" => query.Where(c => !_context.Purchases.Any(p => p.customerId == c.id)),
                    _ => query
                };
            }

            var totalSize = await query.CountAsync();

            IQueryable<Customer> ordered = query.OrderBy(c => c.id);
            if (page != -1)
            {
                ordered = ordered.Skip((page - 1) * size).Take(size);
            }

            var items = await ordered.Select(c => new CustomerListItemDto
            {
                id = c.id,
                name = c.name,
                lastName = c.lastName,
                phone = c.phone,
                mail = c.mail,
                status = _context.Purchases.Any(p => p.customerId == c.id && p.startDate <= now && p.endDate >= now)
                    ? "ActiveSubscriber"
                    : _context.Purchases.Any(p => p.customerId == c.id)
                        ? "Subscriber"
                        : "Customer"
            }).ToListAsync();

            return new PagedResult<CustomerListItemDto>
            {
                items = items,
                page = page,
                size = size,
                totalSize = totalSize,
                totalPages = page == -1 ? 1 : (int)Math.Ceiling((double)totalSize / size)
            };
        }

        public async Task<Customer> Create(Customer customer)
        {
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            return customer;
        }

        public async Task<Customer> Update(CustomerUpdateDto dto)
        {
            var customer = await GetCustomerById(dto.id)
                ?? throw new NotFoundException("Müşteri bulunamadı");

            if (customer.id == SystemGuestId)
            {
                throw new BusinessRuleException("Sistem Misafiri kaydı değiştirilemez!");
            }

            customer.name = dto.name;
            customer.lastName = dto.lastName;
            customer.phone = dto.phone;
            customer.mail = dto.mail;

            customer.updatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return customer;
        }

        public async Task<Customer> Delete(int id)
        {
            var customer = await GetCustomerById(id)
                ?? throw new NotFoundException("Müşteri bulunamadı");

            if (customer.id == SystemGuestId)
            {
                throw new BusinessRuleException("Sistem Misafiri kaydı silinemez!");
            }

            // Aktif/Beklemede oturumu olan bir çocuk varsa engelle.
            var hasActiveBooking = await _context.Bookings
                .AnyAsync(b => _context.Children.Any(ch => ch.parentId == customer.id && ch.id == b.childId)
                          && (b.status == BookingStatus.Active || b.status == BookingStatus.Paused));

            if (hasActiveBooking)
            {
                throw new BusinessRuleException("Bu ebeveynin bir çocuğu şu an içeride aktif oturumda. Oturum kapanmadan silinemez!");
            }

            // Çocukları cascade soft-delete.
            var children = await _context.Children.Where(ch => ch.parentId == customer.id && !ch.isDeleted).ToListAsync();
            foreach (var child in children)
            {
                child.isDeleted = true;
                child.updatedAt = DateTime.UtcNow;
            }

            customer.isDeleted = true;
            customer.updatedAt = DateTime.UtcNow;

            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();
            return customer;
        }
    }
}
