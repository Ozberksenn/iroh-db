using Iroh.Models.DTOs.PurchasePayment;
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
    public class PurchasePaymentController : ControllerBase
    {
        private readonly PurchasePaymentService _purchasePaymentService;

        public PurchasePaymentController(PurchasePaymentService purchasePaymentService)
        {
            _purchasePaymentService = purchasePaymentService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var purchasePayments = _purchasePaymentService.GetAll();
            return Ok(ApiResponse.Ok(purchasePayments, "Başarılı"));
        }

        [HttpPost]
        public IActionResult Create(PurchasePaymentCreateDto dto)
        {
            var purchasePayment = new PurchasePayment
            {
                hours = dto.hours,
                price = dto.price,
                purchaseId = dto.purchaseId
            };
            _purchasePaymentService.Create(purchasePayment);
            return Ok(ApiResponse.Ok(purchasePayment, "Başarıyla oluşturuldu"));
        }
    }
}
