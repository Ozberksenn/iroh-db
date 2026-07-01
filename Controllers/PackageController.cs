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
        private readonly IPackageService _packageService;

        public PackageController(IPackageService packageService)
        {
            _packageService = packageService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _packageService.GetAll();
            return Ok(ApiResponse.Ok(result.Select(PackageDto.From).ToList(), "Başarılı"));
        }

        // Paket tanımlarını yalnızca Admin yönetebilir. GET açık: kredi/abonelik satarken
        // operatörün paket listesine erişmesi gerekir.
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create(PackageCreateDto dto)
        {
            var package = new Package
            {
                Name = dto.Name,
                Hours = dto.Hours,
                Price = dto.Price,
                ValidityDays = dto.ValidityDays
            };
            var result = await _packageService.Create(package);
            return Ok(ApiResponse.Ok(PackageDto.From(result), "Paket başarıyla oluşturuldu"));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut]
        public async Task<IActionResult> Update(PackageUpdateDto dto)
        {
            var package = new Package
            {
                Id = dto.Id,
                Name = dto.Name,
                Hours = dto.Hours,
                Price = dto.Price,
                ValidityDays = dto.ValidityDays
            };
            // Bulunamazsa servis NotFoundException atar → handler 404.
            await _packageService.Update(package);
            return Ok(ApiResponse.Ok(PackageDto.From(package), "Paket başarıyla güncellendi"));
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _packageService.Delete(id);
            return Ok(ApiResponse.Ok<object?>(null, "Paket başarıyla silindi"));
        }
    }
}
