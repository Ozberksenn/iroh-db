
namespace Iroh.Services
{
    public class CompanyService
    {
        private readonly AppDbContext _context;
        public CompanyService(AppDbContext context)
        {
            _context = context;
        }
        public List<Models.Entities.Company> GetAll()
        {
            return _context.Company.ToList();
        }
    }
}