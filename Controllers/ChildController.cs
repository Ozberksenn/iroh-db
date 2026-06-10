using Iroh.Models.CustomResponses;
using Iroh.Models.DTOs.Child;
using Iroh.Models.Entities;
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
            return Ok(results); // Node.js projesinde doğrudan dizi dönülmüş
        }

        [HttpPost("customers/{parentId}")]
        public async Task<IActionResult> Create(long parentId, [FromBody] ChildCreateDto dto)
        {
            try
            {
                var child = await _childService.CreateChild(parentId, dto.name, dto.birthDate);
                return CreatedAtAction(nameof(Create), new { id = child?.id }, child);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("customers/{parentId}")]
        public async Task<IActionResult> GetByParent(long parentId)
        {
            var children = await _childService.GetChildrenByParentId(parentId);
            return Ok(children);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] ChildUpdateDto dto)
        {
            try
            {
                await _childService.UpdateChild(dto.id, dto.name, dto.birthDate);
                return Ok(new { message = "Çocuk başarıyla güncellendi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                await _childService.DeleteChild(id);
                return Ok(new { message = "Çocuk başarıyla silindi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
