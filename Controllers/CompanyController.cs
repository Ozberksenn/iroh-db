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
        public IActionResult Get()
        {
            var companies = _companyService.GetAll();
            return Ok(ApiResponse.Ok(companies, "Başarılı"));
        }

        [HttpPut]
        public IActionResult Update(CompanyUpdateDto dto)
        {
            var company = _companyService.GetCompanyById(dto.id);
            if (company == null)
            {
                throw new NotFoundException("Şirket bulunamadı");
            }

            company.name = dto.name;
            company.firstHourPrice = dto.firstHourPrice;
            company.additionalHalfHourPrice = dto.additionalHalfHourPrice;

            _companyService.Update(company);
            return Ok(ApiResponse.Ok(company, "Bilgiler Başarıyla Güncellendi."));
        }
    }
}
