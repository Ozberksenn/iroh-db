using Iroh.Models.CustomResponses;
using Iroh.Models.DTOs.Purchase;
using Iroh.Models.Entities;
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
        private readonly PurchaseService _purchaseService;

        public PurchaseController(PurchaseService purchaseService)
        {
            _purchaseService = purchaseService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var purchases = _purchaseService.GetAll();
            var response = new CustomResponse<List<Purchase>>(true, "Başarılı", purchases);
            return Ok(response);
        }

        [HttpGet("customer")]
        public async Task<IActionResult> GetByCustomer([FromQuery] long customerId)
        {
            var results = await _purchaseService.GetByCustomerId(customerId);
            return Ok(new CustomResponse<List<CustomerPurchaseResultDto>>(true, "Başarılı", results));
        }

        [HttpGet("purchase-bookings-by-id")]
        public async Task<IActionResult> GetPurchaseBookings([FromQuery] long purchaseId)
        {
            var results = await _purchaseService.GetPurchaseBookings(purchaseId);
            return Ok(new CustomResponse<List<PurchaseBookingResultDto>>(true, "Başarılı", results));
        }

        [HttpPost]
        public async Task<IActionResult> Create(PurchaseCreateDto purchaseCreateDto)
        {
            try
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
                return Ok(new CustomResponse<string>(true, "Paket başarıyla oluşturuldu", null));
            }
            catch (Exception ex)
            {
                return BadRequest(new CustomResponse<string>(false, ex.Message, null));
            }
        }

        [HttpPut]
        public async Task<IActionResult> Update(PurchaseUpdateDto purchaseUpdateDto)
        {
            try
            {
                await _purchaseService.Update(purchaseUpdateDto.id, purchaseUpdateDto.hours, purchaseUpdateDto.price, purchaseUpdateDto.customerId, purchaseUpdateDto.startDate, purchaseUpdateDto.endDate);
                return Ok(new CustomResponse<string>(true, "Paket başarıyla güncellendi", null));
            }
            catch (Exception ex)
            {
                return BadRequest(new CustomResponse<string>(false, ex.Message, null));
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                await _purchaseService.Delete(id);
                return Ok(new CustomResponse<string>(true, "Paket başarıyla silindi", null));
            }
            catch (Exception ex)
            {
                // SQL Prosedüründen gelen "silinemez" hatası burada yakalanacak
                return BadRequest(new CustomResponse<string>(false, ex.Message, null));
            }
        }
    }
}
