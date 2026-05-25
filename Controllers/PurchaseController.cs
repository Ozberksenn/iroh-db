

using Iroh.Models.CustomResponses;
using Iroh.Models.DTOs.Purchase;
using Iroh.Models.Entities;
using Iroh.Services;
using Microsoft.AspNetCore.Mvc;

namespace Iroh.Controllers
{
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
            var response = new CustomResponse<List<Purchase>>(true, "Başırlı", purchases);
            return Ok(response);
        }

        [HttpPost]
        public IActionResult Create(PurchaseCreateDto purchaseCreateDto)
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
            var createdPurchase = _purchaseService.Create(purchase);
            var response = new CustomResponse<Purchase>(true, "Başarılı", createdPurchase);
            return Ok(response);
        }

        [HttpPut("{id}")]
        public IActionResult Update(int id, PurchaseUpdateDto purchaseUpdateDto)
        {
            try
            {
                var purchase = _purchaseService.GetById(id);
                if (purchase == null)
                {
                    var errorResponse = new CustomResponse<Purchase>(false, "Paket bulunamadı!", null);
                    return NotFound(errorResponse);
                }

                var updatePurchase = _purchaseService.Update(
                    purchase, 
                    purchaseUpdateDto.hours, 
                    purchaseUpdateDto.price, 
                    purchaseUpdateDto.customerId, 
                    purchaseUpdateDto.startDate, 
                    purchaseUpdateDto.endDate
                );

                var response = new CustomResponse<Purchase>(true, "Başarılı", updatePurchase);
                return Ok(response);
            }
            catch (KeyNotFoundException)
            {
                var errorResponse = new CustomResponse<Purchase>(false, "Paket bulunamadı!", null);
                return NotFound(errorResponse);
            }
            catch (InvalidOperationException ex)
            {
                var errorResponse = new CustomResponse<Purchase>(false, ex.Message, null);
                return BadRequest(errorResponse);
            }
        }
    }
}