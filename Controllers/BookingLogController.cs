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
        private readonly BookingLogService _bookingLogService;

        public BookingLogController(BookingLogService bookingLogService)
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
                bookingId = dto.bookingId,
                time = dto.time,
                type = dto.type,
                userId = dto.userId
            };
            var result = await _bookingLogService.Create(bookingLog);
            return Ok(ApiResponse.Ok(BookingLogDto.From(result), "Booking Log Başarıyla Oluşturuldu."));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] BookingLogUpdateDto dto)
        {
            // Kayıt yoksa servis NotFoundException atar → handler 404.
            var result = await _bookingLogService.Update(id, dto);
            return Ok(ApiResponse.Ok(BookingLogDto.From(result), "Booking Log Başarıyla Güncellendi."));
        }
    }
}
