using Iroh.Models.DTOs.Package;
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
    public class PackageController : ControllerBase
    {
        private readonly PackageService _packageService;

        public PackageController(PackageService packageService)
        {
            _packageService = packageService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _packageService.GetAll();
            return Ok(ApiResponse.Ok(result.Select(PackageDto.From).ToList(), "Başarılı"));
        }

        [HttpPost]
        public async Task<IActionResult> Create(PackageCreateDto dto)
        {
            var package = new Package
            {
                name = dto.name,
                hours = dto.hours,
                price = dto.price,
                validityDays = dto.validityDays
            };
            var result = await _packageService.Create(package);
            return Ok(ApiResponse.Ok(PackageDto.From(result), "Paket başarıyla oluşturuldu"));
        }

        [HttpPut]
        public async Task<IActionResult> Update(PackageUpdateDto dto)
        {
            var package = new Package
            {
                id = dto.id,
                name = dto.name,
                hours = dto.hours,
                price = dto.price,
                validityDays = dto.validityDays
            };
            // Bulunamazsa servis NotFoundException atar → handler 404.
            await _packageService.Update(package);
            return Ok(ApiResponse.Ok(PackageDto.From(package), "Paket başarıyla güncellendi"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _packageService.Delete(id);
            return Ok(ApiResponse.Ok<object?>(null, "Paket başarıyla silindi"));
        }
    }
}
