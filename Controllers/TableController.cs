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
        private readonly ITableService _tableService;

        public TableController(ITableService tableService)
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
        public async Task<IActionResult> Create(TableCreateDto tableCreateDto)
        {
            var table = new Table { Name = tableCreateDto.Name };
            var createdTable = await _tableService.Create(table);
            return Ok(ApiResponse.Ok(new TableDto { Id = createdTable.Id, Name = createdTable.Name }, "Masa başarıyla oluşturuldu"));
        }

        [HttpPut]
        public async Task<IActionResult> Update(TableUpdateDto tableUpdateDto)
        {
            // Kayıt yoksa servis NotFoundException atar → handler 404.
            var updatedTable = await _tableService.Update(tableUpdateDto);
            return Ok(ApiResponse.Ok(new TableDto { Id = updatedTable.Id, Name = updatedTable.Name }, "Masa başarıyla güncellendi"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            // Aktif rezervasyon → servis BusinessRuleException atar → handler 400.
            await _tableService.Delete(id);
            return Ok(ApiResponse.Ok<object?>(null, "Masa başarıyla silindi"));
        }
    }
}
