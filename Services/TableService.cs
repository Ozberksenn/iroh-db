using Iroh.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public class TableService
    {
        private readonly AppDbContext _context;
        public TableService(AppDbContext context)
        {
            _context = context;
        }

        public List<Table> GetAll()
        {
            return _context.Table.Where(t => !t.isDeleted).ToList();
        }

        public Table Create(Table table)
        {
            _context.Table.Add(table);
            _context.SaveChanges();
            return table;
        }

        public Table Update(Table table)
        {
            _context.Table.Update(table);
            _context.SaveChanges();
            return table;
        }

        public async Task Delete(long id)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($"CALL usp_delete_table({id})");
        }

        public Table? GetById(int id)
        {
            return _context.Table.FirstOrDefault(t => t.id == id && !t.isDeleted);
        }
    }
}
