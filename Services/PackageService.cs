using Iroh.Models.Entities;
using Iroh.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public interface IPackageService
    {
        Task<List<Package>> GetAll();
        Task<Package?> GetById(int id);
        Task<Package> Create(Package package);
        Task Update(Package package);
        Task Delete(int id);
    }

    public class PackageService : IPackageService
    {
        private readonly AppDbContext _context;

        public PackageService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Package>> GetAll()
        {
            return await _context.Packages
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<Package?> GetById(int id)
        {
            return await _context.Packages.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        }

        public async Task<Package> Create(Package package)
        {
            package.CreatedAt = DateTime.UtcNow;
            package.UpdatedAt = DateTime.UtcNow;
            package.IsDeleted = false;

            _context.Packages.Add(package);
            await _context.SaveChangesAsync();
            return package;
        }

        public async Task Update(Package package)
        {
            var existing = await _context.Packages.FirstOrDefaultAsync(p => p.Id == package.Id && !p.IsDeleted);
            if (existing == null)
            {
                throw new NotFoundException("Paket bulunamadı veya silinmiş!");
            }

            existing.Name = package.Name;
            existing.Hours = package.Hours;
            existing.Price = package.Price;
            existing.ValidityDays = package.ValidityDays;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task Delete(int id)
        {
            var package = await _context.Packages.FindAsync(id);
            if (package == null)
            {
                throw new NotFoundException("Paket bulunamadı!");
            }

            package.IsDeleted = true;
            package.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}