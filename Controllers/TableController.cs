using Iroh.Exceptions;
using Iroh.Models.DTOs.Table;
using Iroh.Models.Entities;
using Iroh.Models.Responses;
using Iroh.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Iroh.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TableController : ControllerBase
    {
        private readonly TableService _tableService;

        public TableController(TableService tableService)
        {
            _tableService = tableService;
        }

        // vw_tables: GET /api/table?name=
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? name = null)
        {
            var tables = await _tableService.GetAll(name);
            return Ok(ApiResponse.Ok(tables, "Başarılı"));
        }

        [HttpPost]
        public IActionResult Create(TableCreateDto tableCreateDto)
        {
            var table = new Table { name = tableCreateDto.name };
            var createdTable = _tableService.Create(table);
            return Ok(ApiResponse.Ok(createdTable, "Masa başarıyla oluşturuldu"));
        }

        [HttpPut]
        public IActionResult Update(TableUpdateDto tableUpdateDto)
        {
            var table = _tableService.GetById(tableUpdateDto.id);
            if (table == null)
            {
                throw new NotFoundException("Masa bulunamadı");
            }
            table.name = tableUpdateDto.name;

            var updatedTable = _tableService.Update(table);
            return Ok(ApiResponse.Ok(updatedTable, "Masa başarıyla güncellendi"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            // Aktif rezervasyon → servis BusinessRuleException atar → handler 400.
            await _tableService.Delete(id);
            return Ok(ApiResponse.Ok<object?>(null, "Masa başarıyla silindi"));
        }
    }
}
