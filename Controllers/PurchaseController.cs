using Iroh.Models.DTOs.Purchase;
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
    public class PurchaseController : ControllerBase
    {
        private readonly IPurchaseService _purchaseService;

        public PurchaseController(IPurchaseService purchaseService)
        {
            _purchaseService = purchaseService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var purchases = await _purchaseService.GetAll();
            return Ok(ApiResponse.Ok(purchases, "Başarılı"));
        }

        [HttpGet("customer")]
        public async Task<IActionResult> GetByCustomer([FromQuery] int customerId)
        {
            var results = await _purchaseService.GetByCustomerId(customerId);
            return Ok(ApiResponse.Ok(results, "Başarılı"));
        }

        [HttpGet("purchase-bookings-by-id")]
        public async Task<IActionResult> GetPurchaseBookings([FromQuery] int purchaseId)
        {
            var results = await _purchaseService.GetPurchaseBookings(purchaseId);
            return Ok(ApiResponse.Ok(results, "Başarılı"));
        }

        [HttpPost]
        public async Task<IActionResult> Create(PurchaseCreateDto purchaseCreateDto)
        {
            var purchase = new Purchase
            {
                hours = purchaseCreateDto.hours,
                price = purchaseCreateDto.price,
                customerId = purchaseCreateDto.customerId,
                startDate = purchaseCreateDto.startDate,
                endDate = purchaseCreateDto.endDate,
                createdAt = DateTime.UtcNow
            };
            await _purchaseService.Create(purchase);
            return Ok(ApiResponse.Ok<object?>(null, "Paket başarıyla oluşturuldu"));
        }

        [HttpPut]
        public async Task<IActionResult> Update(PurchaseUpdateDto purchaseUpdateDto)
        {
            await _purchaseService.Update(purchaseUpdateDto);
            return Ok(ApiResponse.Ok<object?>(null, "Paket başarıyla güncellendi"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            // usp_delete_purchase davranışı: silme güvenlik gereği engelli → servis BusinessRuleException atar → 400.
            await _purchaseService.Delete(id);
            return Ok(ApiResponse.Ok<object?>(null, "Paket başarıyla silindi"));
        }
    }
}
