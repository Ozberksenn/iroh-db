using Iroh.Exceptions;
using Iroh.Models.DTOs.Company;
using Iroh.Models.Responses;
using Iroh.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Iroh.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CompanyController : ControllerBase
    {
        private readonly CompanyService _companyService;

        public CompanyController(CompanyService companyService)
        {
            _companyService = companyService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var companies = await _companyService.GetAll();
            return Ok(ApiResponse.Ok(companies.Select(CompanyDto.From).ToList(), "Başarılı"));
        }

        [HttpPut]
        public async Task<IActionResult> Update(CompanyUpdateDto dto)
        {
            var company = await _companyService.GetCompanyById(dto.id);
            if (company == null)
            {
                throw new NotFoundException("Şirket bulunamadı");
            }

            company.name = dto.name;
            company.firstHourPrice = dto.firstHourPrice;
            company.additionalHalfHourPrice = dto.additionalHalfHourPrice;

            await _companyService.Update(company);
            return Ok(ApiResponse.Ok(CompanyDto.From(company), "Bilgiler Başarıyla Güncellendi."));
        }
    }
}
