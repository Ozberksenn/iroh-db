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
            await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        // Update/Delete'in iç kullanımı — soft-delete filtresi uygulamaz.
        public async Task<Customer?> GetCustomerById(int id) =>
            await _context.Customers.FirstOrDefaultAsync(c => c.Id == id);

        // fn_get_customers paritesi: hesaplanmış abone statüsü + serbest metin arama + sayfalama.
        public async Task<PagedResult<CustomerListItemDto>> GetCustomers(string? status, int page, int size, string? name)
        {
            var now = DateTime.UtcNow;

            var query = _context.Customers.Where(c => !c.IsDeleted && c.Id != SystemGuestId);

            if (!string.IsNullOrWhiteSpace(name))
            {
                var pattern = $"%{name}%";
                query = query.Where(c =>
                    EF.Functions.ILike(
                        c.Name + " " + (c.LastName ?? "") + " " + (c.Phone ?? "") + " " + (c.Mail ?? ""),
                        pattern)
                    || _context.Children.Any(ch => ch.ParentId == c.Id && EF.Functions.ILike(ch.Name, pattern)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = status switch
                {
                    "ActiveSubscriber" => query.Where(c =>
                        _context.Purchases.Any(p => p.CustomerId == c.Id && p.StartDate <= now && p.EndDate >= now)),
                    "Subscriber" => query.Where(c =>
                        _context.Purchases.Any(p => p.CustomerId == c.Id)
                        && !_context.Purchases.Any(p => p.CustomerId == c.Id && p.StartDate <= now && p.EndDate >= now)),
                    "Customer" => query.Where(c => !_context.Purchases.Any(p => p.CustomerId == c.Id)),
                    _ => query
                };
            }

            var totalSize = await query.CountAsync();

            IQueryable<Customer> ordered = query.OrderBy(c => c.Id);
            if (page != -1)
            {
                ordered = ordered.Skip((page - 1) * size).Take(size);
            }

            var items = await ordered.Select(c => new CustomerListItemDto
            {
                Id = c.Id,
                Name = c.Name,
                LastName = c.LastName,
                Phone = c.Phone,
                Mail = c.Mail,
                Status = _context.Purchases.Any(p => p.CustomerId == c.Id && p.StartDate <= now && p.EndDate >= now)
                    ? "ActiveSubscriber"
                    : _context.Purchases.Any(p => p.CustomerId == c.Id)
                        ? "Subscriber"
                        : "Customer"
            }).ToListAsync();

            return new PagedResult<CustomerListItemDto>
            {
                Items = items,
                Page = page,
                Size = size,
                TotalSize = totalSize,
                TotalPages = page == -1 ? 1 : (int)Math.Ceiling((double)totalSize / size)
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
            var customer = await GetCustomerById(dto.Id)
                ?? throw new NotFoundException("Müşteri bulunamadı");

            if (customer.Id == SystemGuestId)
            {
                throw new BusinessRuleException("Sistem Misafiri kaydı değiştirilemez!");
            }

            customer.Name = dto.Name;
            customer.LastName = dto.LastName;
            customer.Phone = dto.Phone;
            customer.Mail = dto.Mail;

            customer.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return customer;
        }

        public async Task<Customer> Delete(int id)
        {
            var customer = await GetCustomerById(id)
                ?? throw new NotFoundException("Müşteri bulunamadı");

            if (customer.Id == SystemGuestId)
            {
                throw new BusinessRuleException("Sistem Misafiri kaydı silinemez!");
            }

            // Aktif/Beklemede oturumu olan bir çocuk varsa engelle.
            var hasActiveBooking = await _context.Bookings
                .AnyAsync(b => _context.Children.Any(ch => ch.ParentId == customer.Id && ch.Id == b.ChildId)
                          && (b.Status == BookingStatus.Active || b.Status == BookingStatus.Paused));

            if (hasActiveBooking)
            {
                throw new BusinessRuleException("Bu ebeveynin bir çocuğu şu an içeride aktif oturumda. Oturum kapanmadan silinemez!");
            }

            // Çocukları cascade soft-delete.
            var children = await _context.Children.Where(ch => ch.ParentId == customer.Id && !ch.IsDeleted).ToListAsync();
            foreach (var child in children)
            {
                child.IsDeleted = true;
                child.UpdatedAt = DateTime.UtcNow;
            }

            customer.IsDeleted = true;
            customer.UpdatedAt = DateTime.UtcNow;

            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();
            return customer;
        }
    }
}
