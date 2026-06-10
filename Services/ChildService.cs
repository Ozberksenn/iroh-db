using Iroh.Models.DTOs.Child;
using Iroh.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Iroh.Services
{
    public class ChildService
    {
        private readonly AppDbContext _context;

        public ChildService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<UnifiedSearchResultDto>> SearchUnified(string search)
        {
            return await _context.UnifiedSearchResults
                .FromSqlInterpolated($"SELECT * FROM fn_search_unified({search})")
                .ToListAsync();
        }

        public async Task<Child?> CreateChild(long parentId, string name, DateTime? birthDate)
        {
            var result = await _context.Children
                .FromSqlInterpolated($"SELECT * FROM fn_insert_child({parentId}, {name}, {birthDate})")
                .ToListAsync();
            
            return result.FirstOrDefault();
        }

        public async Task<List<Child>> GetChildrenByParentId(long parentId)
        {
            return await _context.Children
                .FromSqlInterpolated($"SELECT * FROM fn_get_children_by_parent_id({parentId})")
                .ToListAsync();
        }

        public async Task UpdateChild(long id, string name, DateTime? birthDate)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($"CALL usp_update_child({id}, {name}, {birthDate})");
        }

        public async Task DeleteChild(long id)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($"CALL usp_delete_child({id})");
        }
        
        public async Task<Child?> GetById(long id)
        {
            return await _context.Children.FirstOrDefaultAsync(c => c.id == id && !c.isDeleted);
        }
    }
}
