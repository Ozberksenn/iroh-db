using Iroh.Models.CustomResponses;
using Iroh.Models.DTOs.Company;
using Iroh.Models.Entities;
using Iroh.Services;
using Microsoft.AspNetCore.Mvc;


namespace Iroh.Controllers
{
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
            var response = new CustomResponse<List<Company>>(true, "Başırlı", companies);
            return Ok(response);
        }

        [HttpPut]
        public IActionResult Update(CompanyUpdateDto dto)
        {
            var company = _companyService.GetCompanyById(dto.id);
            if (company == null)
            {
                return NotFound();
            }

            company.name = dto.name;
            company.firstHourPrice = dto.firstHourPrice;
            company.additionalHalfHourPrice = dto.additionalHalfHourPrice;

            _companyService.Update(company);
            var response = new CustomResponse<Company>(true, "Bilgiler Başarıyla Güncellendi.", company);
            return Ok(response);
        }
    }
}