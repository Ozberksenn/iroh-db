using Iroh.Models.CustomResponses;
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
            var response = new CustomResponse<List<Company>>(true, "Masalar başarıyla getirildi", companies);
            return Ok(response);
        }
    }
}