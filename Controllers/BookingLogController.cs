
using Iroh.Models.CustomResponses;
using Iroh.Models.Entities;
using Iroh.Services;
using Microsoft.AspNetCore.Mvc;

namespace Iroh.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class BookingLogController : ControllerBase
    {
        private readonly BookingLogService _bookingLogService;

        public BookingLogController(BookingLogService bookingLogService)
        {
            _bookingLogService = bookingLogService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var bookingLogs = _bookingLogService.GetAll();
            var response = new CustomResponse<List<BookingLog>>(true, "Başırlı", bookingLogs);
            return Ok(response);
        }
    }

}