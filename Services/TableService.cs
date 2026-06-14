using Iroh.Models.Entities;
using Iroh.Models.DTOs.Table;
using Iroh.Exceptions;
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

        // vw_tables: id, name (isdeleted=false) + opsiyonel name ILIKE filtresi.
        public async Task<List<TableDto>> GetAll(string? name)
        {
            var query = _context.Table.Where(t => !t.isDeleted);
            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(t => EF.Functions.ILike(t.name, "%" + name + "%"));
            }
            return await query.Select(t => new TableDto { id = t.id, name = t.name }).ToListAsync();
        }

        public async Task<Table> Create(Table table)
        {
            _context.Table.Add(table);
            await _context.SaveChangesAsync();
            return table;
        }

        public async Task<Table> Update(Table table)
        {
            _context.Table.Update(table);
            await _context.SaveChangesAsync();
            return table;
        }

        public async Task Delete(long id)
        {
            // aktif booking kontrolü (usp_delete_table logic)
            var hasActiveBooking = await _context.Booking.AnyAsync(b => b.tableId == (int)id && (b.status == Models.Enums.BookingStatus.Active || b.status == Models.Enums.BookingStatus.Paused));
            if (hasActiveBooking)
            {
                throw new BusinessRuleException("Bu masaya ait aktif rezervasyon var. Silinemez!");
            }

            var table = await _context.Table.FindAsync((int)id);
            if (table != null)
            {
                table.isDeleted = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Table?> GetById(int id) =>
            await _context.Table.FirstOrDefaultAsync(t => t.id == id && !t.isDeleted);
    }
}
