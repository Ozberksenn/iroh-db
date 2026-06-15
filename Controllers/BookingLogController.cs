using Iroh.Models.DTOs.Booking;
using Iroh.Models.DTOs.BookingLog;
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
    public class BookingLogController : ControllerBase
    {
        private readonly IBookingLogService _bookingLogService;

        public BookingLogController(IBookingLogService bookingLogService)
        {
            _bookingLogService = bookingLogService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var bookingLogs = await _bookingLogService.GetAll();
            return Ok(ApiResponse.Ok(bookingLogs.Select(BookingLogDto.From).ToList(), "Başarılı"));
        }

        [HttpPost]
        public async Task<IActionResult> Create(BookingLogInsertDto dto)
        {
            var bookingLog = new BookingLog
            {
                BookingId = dto.BookingId,
                Time = dto.Time,
                Type = dto.Type,
                UserId = dto.UserId
            };
            var result = await _bookingLogService.Create(bookingLog);
            return Ok(ApiResponse.Ok(BookingLogDto.From(result), "Booking Log Başarıyla Oluşturuldu."));
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] BookingLogUpdateDto dto)
        {
            // Kayıt yoksa servis NotFoundException atar → handler 404.
            var result = await _bookingLogService.Update(dto);
            return Ok(ApiResponse.Ok(BookingLogDto.From(result), "Booking Log Başarıyla Güncellendi."));
        }
    }
}
