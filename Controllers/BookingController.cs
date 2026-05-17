
using Iroh.Models.CustomResponses;
using Iroh.Models.Entities;
using Iroh.Services;
using Microsoft.AspNetCore.Mvc;

namespace Iroh.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class BookingController : ControllerBase
    {
        private readonly BookingService _bookingService;

        public BookingController(BookingService bookingService)
        {
            _bookingService = bookingService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var bookings = _bookingService.GetAll();
            var response = new CustomResponse<List<Booking>>(true, "Başırlı", bookings);
            return Ok(response);
        }
    }

}