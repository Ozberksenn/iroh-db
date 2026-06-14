using Iroh.Models.CustomResponses;
using Iroh.Models.Entities;
using Iroh.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Iroh.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/packages")]
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
            return Ok(new CustomResponse<List<Package>>(true, "Başarılı", result));
        }

        [HttpPost]
        public async Task<IActionResult> Create(Package package)
        {
            try
            {
                var result = await _packageService.Create(package);
                return Ok(new CustomResponse<Package>(true, "Paket başarıyla oluşturuldu", result));
            }
            catch (Exception ex)
            {
                return BadRequest(new CustomResponse<string>(false, ex.Message, null));
            }
        }

        [HttpPut]
        public async Task<IActionResult> Update(Package package)
        {
            try
            {
                await _packageService.Update(package);
                return Ok(new CustomResponse<string>(true, "Paket başarıyla güncellendi", null));
            }
            catch (Exception ex)
            {
                return BadRequest(new CustomResponse<string>(false, ex.Message, null));
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _packageService.Delete(id);
                return Ok(new CustomResponse<string>(true, "Paket başarıyla silindi", null));
            }
            catch (Exception ex)
            {
                return BadRequest(new CustomResponse<string>(false, ex.Message, null));
            }
        }
    }
}