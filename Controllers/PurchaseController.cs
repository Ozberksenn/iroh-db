

using Iroh.Models.CustomResponses;
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
    }
}