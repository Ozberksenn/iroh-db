
using Iroh.Models.Entities;

namespace Iroh.Services
{
    public class CompanyService
    {
        private readonly AppDbContext _context;
        public CompanyService(AppDbContext context)
        {
            _context = context;
        }
        public List<Company> GetAll()
        {
            return _context.Company.ToList();
        }
        public Company? GetCompanyById(int id)
        {
            return _context.Company.FirstOrDefault(c => c.id == id);
        }
        public Company Update(Company company)
        {
            _context.Company.Update(company);
            _context.SaveChanges();
            return company;
        }
    }
}