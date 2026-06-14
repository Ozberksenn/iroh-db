using Iroh.Models.Entities;
using Iroh.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public class PackageService
    {
        private readonly AppDbContext _context;

        public PackageService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Package>> GetAll()
        {
            return await _context.Packages
                .Where(p => !p.isDeleted)
                .OrderByDescending(p => p.createdAt)
                .ToListAsync();
        }

        public async Task<Package?> GetById(int id)
        {
            return await _context.Packages.FirstOrDefaultAsync(p => p.id == id && !p.isDeleted);
        }

        public async Task<Package> Create(Package package)
        {
            package.createdAt = DateTime.Now;
            package.updatedAt = DateTime.Now;
            package.isDeleted = false;

            _context.Packages.Add(package);
            await _context.SaveChangesAsync();
            return package;
        }

        public async Task Update(Package package)
        {
            var existing = await _context.Packages.FirstOrDefaultAsync(p => p.id == package.id && !p.isDeleted);
            if (existing == null)
            {
                throw new NotFoundException("Paket bulunamadı veya silinmiş!");
            }

            existing.name = package.name;
            existing.hours = package.hours;
            existing.price = package.price;
            existing.validityDays = package.validityDays;
            existing.updatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
        }

        public async Task Delete(int id)
        {
            var package = await _context.Packages.FindAsync(id);
            if (package == null)
            {
                throw new NotFoundException("Paket bulunamadı!");
            }

            package.isDeleted = true;
            package.updatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
        }
    }
}