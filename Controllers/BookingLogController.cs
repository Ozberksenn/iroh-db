using Iroh.Exceptions;
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
            return Ok(ApiResponse.Ok(bookingLogs, "Başarılı"));
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
            return Ok(ApiResponse.Ok(result, "Booking Log Başarıyla Oluşturuldu."));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] BookingLogUpdateDto dto)
        {
            var bookingLog = await _bookingLogService.GetById(id);
            if (bookingLog == null)
            {
                throw new NotFoundException("Kayıt bulunamadı");
            }

            bookingLog.bookingId = dto.bookingId;
            bookingLog.time = dto.time;
            bookingLog.type = dto.type;
            bookingLog.userId = dto.userId;

            var result = await _bookingLogService.Update(bookingLog);
            return Ok(ApiResponse.Ok(result, "Booking Log Başarıyla Güncellendi."));
        }
    }
}
