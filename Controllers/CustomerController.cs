using Iroh.Models.CustomResponses;
using Iroh.Models.DTOs.Company;
using Iroh.Models.DTOs.Customer;
using Iroh.Models.Entities;
using Iroh.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace Iroh.Controllers
{
    [Authorize]
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

        [HttpPut]
        public IActionResult Update([FromBody] CustomerUpdateDto customerUpdateDto)
        {
            var customer = _customerService.GetCustomerById(customerUpdateDto.id);
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

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var customer = _customerService.GetCustomerById(id);
            if (customer == null)
            {
                var errorResponse = new CustomResponse<Customer>(false, "Müşteri bulunamadı", null);
                return NotFound(errorResponse);
            }

            try
            {
                var deletedCustomer = _customerService.Delete(customer);
                var response = new CustomResponse<Customer>(true, "Müşteri başarıyla silindi", deletedCustomer);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                var errorResponse = new CustomResponse<Customer>(false, ex.Message, null);
                return BadRequest(errorResponse);
            }
        }
    }
}