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
            // aktif booking kontrolü (usp_delete_table logic)
            var hasActiveBooking = await _context.Booking.AnyAsync(b => b.tableId == (int)id && (b.status == Models.Enums.BookingStatus.Active || b.status == Models.Enums.BookingStatus.Paused));
            if (hasActiveBooking)
            {
                throw new Exception("Bu masaya ait aktif rezervasyon var. Silinemez!");
            }

            var table = await _context.Table.FindAsync((int)id);
            if (table != null)
            {
                table.isDeleted = true;
                await _context.SaveChangesAsync();
            }
        }

        public Table? GetById(int id)
        {
            return _context.Table.FirstOrDefault(t => t.id == id && !t.isDeleted);
        }
    }
}
