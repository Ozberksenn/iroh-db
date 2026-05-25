using Iroh.Models.CustomResponses;
using Iroh.Models.DTOs.Company;
using Iroh.Models.DTOs.Customer;
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
            var response = new CustomResponse<List<Customer>>(true, "Başarılı", customers);
            return Ok(response);
        }

        [HttpPost]
        public IActionResult Create(CustomerCreateDto customerCreateDto)
        {
            var customer = new Customer
            {
                name = customerCreateDto.name,
                lastName = customerCreateDto.lastName,
                phone = customerCreateDto.phone,
                mail = customerCreateDto.mail
            };
            var createdCustomer = _customerService.Create(customer);
            var response = new CustomResponse<Customer>(true, "Müşteri başarıyla oluşturuldu", createdCustomer);
            return Ok(response);
        }

        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] CustomerUpdateDto customerUpdateDto)
        {
            var customer = _customerService.GetCustomerById(id);
            if (customer == null)
            {
                return NotFound();
            }
            customer.name = customerUpdateDto.name;
            customer.lastName = customerUpdateDto.lastName;
            customer.phone = customerUpdateDto.phone;
            customer.mail = customerUpdateDto.mail;
            customer.updatedAt = DateTime.UtcNow;
            customer.isDeleted = customer.isDeleted; // isDeleted güncellenmeyecek, mevcut değeri koruyoruz

            var updatedCustomer = _customerService.Update(customer);
            var response = new CustomResponse<Customer>(true, "Müşteri başarıyla güncellendi", updatedCustomer);
            return Ok(response);
        }

    }
}