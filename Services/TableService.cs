using Iroh.Models.Entities;
using Iroh.Models.DTOs.Table;
using Iroh.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public interface ITableService
    {
        Task<List<TableDto>> GetAll(string? name);
        Task<Table> Create(Table table);
        Task<Table> Update(TableUpdateDto dto);
        Task Delete(int id);
        Task<Table?> GetById(int id);
    }

    public class TableService : ITableService
    {
        private readonly AppDbContext _context;
        public TableService(AppDbContext context)
        {
            _context = context;
        }

        // vw_tables: id, name (isdeleted=false) + opsiyonel name ILIKE filtresi.
        public async Task<List<TableDto>> GetAll(string? name)
        {
            var query = _context.Tables.Where(t => !t.IsDeleted);
            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(t => EF.Functions.ILike(t.Name, "%" + name + "%"));
            }
            return await query.Select(t => new TableDto { Id = t.Id, Name = t.Name }).ToListAsync();
        }

        public async Task<Table> Create(Table table)
        {
            _context.Tables.Add(table);
            await _context.SaveChangesAsync();
            return table;
        }

        public async Task<Table> Update(TableUpdateDto dto)
        {
            var table = await GetById(dto.Id)
                ?? throw new NotFoundException("Masa bulunamadı");

            table.Name = dto.Name;
            await _context.SaveChangesAsync();
            return table;
        }

        public async Task Delete(int id)
        {
            // aktif booking kontrolü (usp_delete_table logic)
            var hasActiveBooking = await _context.Bookings.AnyAsync(b => b.TableId == id && (b.Status == Models.Enums.BookingStatus.Active || b.Status == Models.Enums.BookingStatus.Paused));
            if (hasActiveBooking)
            {
                throw new BusinessRuleException("Bu masaya ait aktif rezervasyon var. Silinemez!");
            }

            var table = await _context.Tables.FindAsync(id);
            if (table != null)
            {
                table.IsDeleted = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Table?> GetById(int id) =>
            await _context.Tables.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
    }
}
