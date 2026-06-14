

using Iroh.Models.CustomResponses;
using Iroh.Models.DTOs.Purchase;
using Iroh.Models.DTOs.PurchasePayment;
using Iroh.Models.Entities;
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
            var response = new CustomResponse<List<PurchasePayment>>(true, "Başırlı", purchasePayments);
            return Ok(response);
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
            var response = new CustomResponse<PurchasePayment>(true, "Başarıyla oluşturuldu", purchasePayment);
            return Ok(response);
        }
    }
}