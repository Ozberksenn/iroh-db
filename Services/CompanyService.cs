using Iroh.Exceptions;
using Iroh.Models.DTOs.Company;
using Iroh.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public interface ICompanyService
    {
        Task<List<Company>> GetAll();
        Task<Company?> GetCompanyById(int id);
        Task<Company> Update(CompanyUpdateDto dto);
    }

    public class CompanyService : ICompanyService
    {
        private readonly AppDbContext _context;
        public CompanyService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Company>> GetAll() =>
            await _context.Companies.AsNoTracking().ToListAsync();

        public async Task<Company?> GetCompanyById(int id) =>
            await _context.Companies.FirstOrDefaultAsync(c => c.Id == id);

        public async Task<Company> Update(CompanyUpdateDto dto)
        {
            var company = await GetCompanyById(dto.Id)
                ?? throw new NotFoundException("Şirket bulunamadı");

            // Finansal/zorunlu alan doğrulaması: sessiz kabul yerine anlamlı hata.
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new BusinessRuleException("Şirket adı boş olamaz.");
            if (dto.FirstHourPrice <= 0)
                throw new BusinessRuleException("İlk saat ücreti 0'dan büyük olmalıdır.");
            if (dto.AdditionalHalfHourPrice < 0)
                throw new BusinessRuleException("Ek yarım saat ücreti negatif olamaz.");

            company.Name = dto.Name;
            company.FirstHourPrice = dto.FirstHourPrice;
            company.AdditionalHalfHourPrice = dto.AdditionalHalfHourPrice;

            await _context.SaveChangesAsync();
            return company;
        }
    }
}
