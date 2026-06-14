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
            await _context.Company.ToListAsync();

        public async Task<Company?> GetCompanyById(int id) =>
            await _context.Company.FirstOrDefaultAsync(c => c.id == id);

        public async Task<Company> Update(Company company)
        {
            _context.Company.Update(company);
            await _context.SaveChangesAsync();
            return company;
        }
    }
}
