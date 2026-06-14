using Iroh.Exceptions;
using Iroh.Models.DTOs.Common;
using Iroh.Models.DTOs.Customer;
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
    public class CustomerController : ControllerBase
    {
        private readonly CustomerService _customerService;

        public CustomerController(CustomerService customerService)
        {
            _customerService = customerService;
        }

        // fn_get_customers
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int size = 50, [FromQuery] string? name = null)
        {
            var result = await _customerService.GetCustomers(status, page, size, name);
            return Ok(ApiResponse.Ok(result, "Başarılı"));
        }

        // fn_get_customer_by_id
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var customer = await _customerService.GetById(id);
            if (customer == null)
            {
                throw new NotFoundException("Müşteri bulunamadı");
            }
            return Ok(ApiResponse.Ok(CustomerDto.From(customer)));
        }

        [HttpPost]
        public async Task<IActionResult> Create(CustomerCreateDto customerCreateDto)
        {
            var customer = new Customer
            {
                name = customerCreateDto.name,
                lastName = customerCreateDto.lastName,
                phone = customerCreateDto.phone,
                mail = customerCreateDto.mail
            };
            var createdCustomer = await _customerService.Create(customer);
            return Ok(ApiResponse.Ok(CustomerDto.From(createdCustomer), "Müşteri başarıyla oluşturuldu"));
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] CustomerUpdateDto customerUpdateDto)
        {
            var customer = await _customerService.GetCustomerById(customerUpdateDto.id);
            if (customer == null)
            {
                throw new NotFoundException("Müşteri bulunamadı");
            }

            customer.name = customerUpdateDto.name;
            customer.lastName = customerUpdateDto.lastName;
            customer.phone = customerUpdateDto.phone;
            customer.mail = customerUpdateDto.mail;

            var updatedCustomer = await _customerService.Update(customer);
            return Ok(ApiResponse.Ok(CustomerDto.From(updatedCustomer), "Müşteri başarıyla güncellendi"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var customer = await _customerService.GetCustomerById(id);
            if (customer == null)
            {
                throw new NotFoundException("Müşteri bulunamadı");
            }

            // Aktif oturum / sistem misafiri → servis BusinessRuleException atar → handler 400.
            var deletedCustomer = await _customerService.Delete(customer);
            return Ok(ApiResponse.Ok(CustomerDto.From(deletedCustomer), "Müşteri başarıyla silindi"));
        }
    }
}
