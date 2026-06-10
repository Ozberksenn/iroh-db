using Iroh.Models.CustomResponses;
using Iroh.Models.DTOs.Table;
using Iroh.Models.Entities;
using Iroh.Services;
using Microsoft.AspNetCore.Mvc;

namespace Iroh.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class TableController : ControllerBase
    {
        private readonly TableService _tableService;

        public TableController(TableService tableService)
        {
            _tableService = tableService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var tables = _tableService.GetAll();
            var response = new CustomResponse<List<Table>>(true, "Başarılı", tables);
            return Ok(response);
        }

        [HttpPost]
        public IActionResult Create(TableCreateDto tableCreateDto)
        {
            var table = new Table
            {
                name = tableCreateDto.name
            };
            var createdTable = _tableService.Create(table);
            var response = new CustomResponse<Table>(true, "Masa başarıyla oluşturuldu", createdTable);
            return Ok(response);
        }

        [HttpPut("{id}")]
        public IActionResult Update(int id, TableUpdateDto tableUpdateDto)
        {
            var table = _tableService.GetById(id);
            if (table == null)
            {
                return NotFound();
            }
            table.name = tableUpdateDto.name;
            table.updatedAt = DateTime.UtcNow;

            var updatedTable = _tableService.Update(table);
            var response = new CustomResponse<Table>(true, "Masa başarıyla güncellendi", updatedTable);
            return Ok(response);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                await _tableService.Delete(id);
                var response = new CustomResponse<string>(true, "Masa başarıyla silindi", null);
                return Ok(response);
            }
            catch (Exception ex)
            {
                // SQL Prosedüründen gelen "aktif rezervasyon var" hatası burada yakalanacak
                var errorResponse = new CustomResponse<string>(false, ex.Message, null);
                return BadRequest(errorResponse);
            }
        }
    }
}
