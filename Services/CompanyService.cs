using Iroh.Exceptions;
using Iroh.Models.DTOs.Company;
using Iroh.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public class CompanyService
    {
        private readonly AppDbContext _context;
        public CompanyService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Company>> GetAll() =>
            await _context.Companies.AsNoTracking().ToListAsync();

        public async Task<Company?> GetCompanyById(int id) =>
            await _context.Companies.FirstOrDefaultAsync(c => c.id == id);

        public async Task<Company> Update(CompanyUpdateDto dto)
        {
            var company = await GetCompanyById(dto.id)
                ?? throw new NotFoundException("Şirket bulunamadı");

            company.name = dto.name;
            company.firstHourPrice = dto.firstHourPrice;
            company.additionalHalfHourPrice = dto.additionalHalfHourPrice;

            await _context.SaveChangesAsync();
            return company;
        }
    }
}
