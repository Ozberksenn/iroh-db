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
        private readonly ChildService _childService;

        public ChildController(ChildService childService)
        {
            _childService = childService;
        }

        [HttpGet("search-unified")]
        public async Task<IActionResult> Search([FromQuery] string q = "")
        {
            var results = await _childService.SearchUnified(q);
            return Ok(ApiResponse.Ok(results, "Başarılı"));
        }

        [HttpPost("customers/{parentId}")]
        public async Task<IActionResult> Create(long parentId, [FromBody] ChildCreateDto dto)
        {
            // Sistem misafiri → servis BusinessRuleException atar → handler 400.
            var child = await _childService.CreateChild(parentId, dto.name, dto.birthDate);
            return Ok(ApiResponse.Ok(child, "Çocuk başarıyla oluşturuldu"));
        }

        [HttpGet("customers/{parentId}")]
        public async Task<IActionResult> GetByParent(long parentId)
        {
            var children = await _childService.GetChildrenByParentId(parentId);
            return Ok(ApiResponse.Ok(children, "Başarılı"));
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] ChildUpdateDto dto)
        {
            await _childService.UpdateChild(dto.id, dto.name, dto.birthDate);
            return Ok(ApiResponse.Ok<object?>(null, "Çocuk başarıyla güncellendi"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            await _childService.DeleteChild(id);
            return Ok(ApiResponse.Ok<object?>(null, "Çocuk başarıyla silindi"));
        }
    }
}
