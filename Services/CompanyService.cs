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

            company.Name = dto.Name;
            company.FirstHourPrice = dto.FirstHourPrice;
            company.AdditionalHalfHourPrice = dto.AdditionalHalfHourPrice;

            await _context.SaveChangesAsync();
            return company;
        }
    }
}
