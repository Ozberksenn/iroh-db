
using Iroh.Services;
using Microsoft.AspNetCore.Mvc;
using Iroh.Models.CustomResponses;
using Iroh.Models.Entities;
using Iroh.Models.DTOs.Table;

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
            var response = new CustomResponse<List<Table>>(true, "Masalar başarıyla getirildi", tables);
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
                var errorResponse = new CustomResponse<string>(false, "Masa bulunamadı", null);
                return NotFound(errorResponse);
            }
            table.name = tableUpdateDto.name;
            var updatedTable = _tableService.Update(table);
            var response = new CustomResponse<Table>(true, "Masa başarıyla güncellendi", updatedTable);
            return Ok(response);
        }

        // [HttpPut("{id}")]
        // public IActionResult Delete(int id)
        // {
        //     var table = _tableService.GetById(id);
        //     if (table == null)
        //     {
        //         var errorResponse = new CustomResponse<string>(false, "Masa bulunamadı", null);
        //         return NotFound(errorResponse);
        //     }
        //     _tableService.Update(table);
        //     var response = new CustomResponse<string>(true, "Masa başarıyla silindi", null);
        //     return Ok(response);
        // }
    }
}