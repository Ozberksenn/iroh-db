using Iroh.Models.DTOs.Child;
using Iroh.Models.Responses;
using Iroh.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Iroh.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ChildController : ControllerBase
    {
        private readonly IChildService _childService;

        public ChildController(IChildService childService)
        {
            _childService = childService;
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q = "")
        {
            var results = await _childService.SearchUnified(q);
            return Ok(ApiResponse.Ok(results, "Başarılı"));
        }

        [HttpPost("parent/{parentId}")]
        public async Task<IActionResult> Create(int parentId, [FromBody] ChildCreateDto dto)
        {
            // Sistem misafiri → servis BusinessRuleException atar → handler 400.
            var child = await _childService.CreateChild(parentId, dto.Name, dto.BirthDate);
            return Ok(ApiResponse.Ok(ChildDto.From(child!), "Çocuk başarıyla oluşturuldu"));
        }

        [HttpGet("parent/{parentId}")]
        public async Task<IActionResult> GetByParent(int parentId)
        {
            var children = await _childService.GetChildrenByParentId(parentId);
            return Ok(ApiResponse.Ok(children.Select(ChildDto.From).ToList(), "Başarılı"));
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] ChildUpdateDto dto)
        {
            await _childService.UpdateChild(dto.Id, dto.Name, dto.BirthDate);
            return Ok(ApiResponse.Ok<object?>(null, "Çocuk başarıyla güncellendi"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _childService.DeleteChild(id);
            return Ok(ApiResponse.Ok<object?>(null, "Çocuk başarıyla silindi"));
        }
    }
}
