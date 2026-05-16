using Iroh.Models.CustomResponses;
using Iroh.Models.DTOs.Company;
using Iroh.Models.Entities;
using Iroh.Services;
using Microsoft.AspNetCore.Mvc;


namespace Iroh.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class CustomerController : ControllerBase
    {
        private readonly CustomerService _customerService;

        public CustomerController(CustomerService customerService)
        {
            _customerService = customerService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var customers = _customerService.GetAll();
            var response = new CustomResponse<List<Customer>>(true, "Başırlı", customers);
            return Ok(response);
        }

    }
}