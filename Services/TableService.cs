

using Iroh.Models.Entities;

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
            return _context.Table.ToList();
        }
        public Table? GetById(int id)
        {
            // _context.Table -> Senin DbSet'in (tablon)
            // .FirstOrDefault -> Şarta uyan İLK kaydı getir, bulamazsan 'null' dön.
            // t => t.id == id -> Lambda ifadesi (Sorgu şartı)
            return _context.Table.FirstOrDefault(t => t.id == id);
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

    }
}