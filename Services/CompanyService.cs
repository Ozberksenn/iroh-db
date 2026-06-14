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
            await _context.Companies.ToListAsync();

        public async Task<Company?> GetCompanyById(int id) =>
            await _context.Companies.FirstOrDefaultAsync(c => c.id == id);

        public async Task<Company> Update(Company company)
        {
            _context.Companies.Update(company);
            await _context.SaveChangesAsync();
            return company;
        }
    }
}
