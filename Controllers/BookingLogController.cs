
using Iroh.Models.CustomResponses;
using Iroh.Models.DTOs.BookingLog;
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

        [HttpPost]
        public IActionResult Create(BookingLogInsertDto dto)
        {
            var bookingLog = new BookingLog
            {
                bookingId = dto.bookingId,
                time = dto.time,
                type = dto.type,
                userId = dto.userId
            };
            var result = _bookingLogService.Create(bookingLog);
            var response = new CustomResponse<BookingLog>(true, "Booking Log Başarıyla Oluşturuldu.", result);
            return Ok(response);
        }

        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] BookingLogUpdateDto dto)
        {
            var bookingLog = _bookingLogService.GetById(id);
            if (bookingLog == null)
            {
                var errorResponse = new CustomResponse<BookingLog>(false, "Kayıt bulunamadı", null);
                return NotFound(errorResponse);
            }

            bookingLog.bookingId = dto.bookingId;
            bookingLog.time = dto.time;
            bookingLog.type = dto.type;
            bookingLog.userId = dto.userId;

            var result = _bookingLogService.Update(bookingLog);
            var response = new CustomResponse<BookingLog>(true, "Booking Log Başarıyla Güncellendi.", result);
            return Ok(response);
        }

    }

}