
using Iroh.Models.CustomResponses;
using Iroh.Models.DTOs.Booking;
using Iroh.Models.Entities;
using Iroh.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Iroh.Controllers
{
    [Authorize]
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

        [HttpGet("Active")]
        public IActionResult GetActive()
        {
            var bookings = _bookingService.GetActiveBookings();
            var response = new CustomResponse<List<Booking>>(true, "Başarılı", bookings);
            return Ok(response);
        }

        [HttpPost]
        public IActionResult Create(BookingCreateDto bookingCreateDto)
        {
            var booking = new Booking
            {
                tableId = bookingCreateDto.tableId,
                startTime = bookingCreateDto.startTime,
                endTime = bookingCreateDto.endTime,
                status = bookingCreateDto.status,
                price = bookingCreateDto.price,
                childId = bookingCreateDto.childId,
                note = bookingCreateDto.note
            };
            var createdBooking = _bookingService.Create(booking);
            var response = new CustomResponse<Booking>(true, "Başarılı", createdBooking);
            return Ok(response);
        }

        [HttpPut]
        public IActionResult Update(BookingUpdateDto bookingUpdateDto)
        {
            var updateBooking = _bookingService.Update(bookingUpdateDto.id, bookingUpdateDto);
            return updateBooking != null
                ? Ok(new CustomResponse<Booking>(true, "Başarılı", updateBooking))
                : NotFound(new CustomResponse<Booking>(false, "Kayıt bulunamadı", null));
        }
    }

}