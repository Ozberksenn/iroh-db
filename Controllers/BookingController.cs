using Iroh.Models.DTOs.Booking;
using Iroh.Models.DTOs.Wallet;
using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Iroh.Models.Responses;
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
        private readonly IBookingService _bookingService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IWalletService _walletService;

        public BookingController(IBookingService bookingService, ISubscriptionService subscriptionService, IWalletService walletService)
        {
            _bookingService = bookingService;
            _subscriptionService = subscriptionService;
            _walletService = walletService;
        }

        private int? CurrentUserId() =>
            int.TryParse(User.FindFirst("id")?.Value, out var id) ? id : (int?)null;

        // usp_get_bookings: GET /api/booking?page=&size=&status=&name=&customerId=&childId=&startTime=&endTime=&tableId=
        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] int page = 1,
            [FromQuery] int size = 20,
            [FromQuery(Name = "status")] string[]? status = null,
            [FromQuery] string? name = null,
            [FromQuery] string? mail = null,
            [FromQuery] int? customerId = null,
            [FromQuery] int? childId = null,
            [FromQuery] DateTime? startTime = null,
            [FromQuery] DateTime? endTime = null,
            [FromQuery] int? tableId = null)
        {
            var result = await _bookingService.GetBookings(page, size, status, name, mail, customerId, childId, startTime, endTime, tableId);
            return Ok(ApiResponse.Ok(result, "Başarılı"));
        }

        // vw_activebookings: abone kademesi + en iyi paket + usedMinutes + payments + logs
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            var bookings = await _subscriptionService.GetActiveBookings();
            return Ok(ApiResponse.Ok(bookings, "Başarılı"));
        }

        [HttpPost]
        public async Task<IActionResult> Create(BookingCreateDto bookingCreateDto)
        {
            var booking = new Booking
            {
                TableId = bookingCreateDto.TableId,
                StartTime = bookingCreateDto.StartTime,
                EndTime = bookingCreateDto.EndTime,
                Status = bookingCreateDto.Status,
                Price = bookingCreateDto.Price,
                ChildId = bookingCreateDto.ChildId,
                Note = bookingCreateDto.Note
            };
            var createdBooking = await _bookingService.Create(booking);
            return Ok(ApiResponse.Ok(BookingDto.From(createdBooking), "Başarılı"));
        }

        [HttpPut]
        public async Task<IActionResult> Update(BookingUpdateDto bookingUpdateDto)
        {
            // Kayıt yoksa servis NotFoundException atar → handler 404.
            var updated = await _bookingService.Update(bookingUpdateDto.Id, bookingUpdateDto);
            return Ok(ApiResponse.Ok(BookingDto.From(updated), "Başarılı"));
        }

        // F2.5: durum değişimi + geçmiş log'u tek transaction'da. userId JWT'den (CurrentUserId).
        [HttpPost("{id}/pause")]
        public async Task<IActionResult> Pause(int id, [FromBody] BookingStatusChangeDto? dto)
        {
            var b = await _bookingService.ChangeStatus(id, BookingStatus.Paused, BookingLogType.Pause,
                dto?.MinutesAgo, dto?.Note, CurrentUserId());
            return Ok(ApiResponse.Ok(BookingDto.From(b), "Seans duraklatıldı"));
        }

        [HttpPost("{id}/resume")]
        public async Task<IActionResult> Resume(int id, [FromBody] BookingStatusChangeDto? dto)
        {
            var b = await _bookingService.ChangeStatus(id, BookingStatus.Active, BookingLogType.Continue,
                dto?.MinutesAgo, dto?.Note, CurrentUserId());
            return Ok(ApiResponse.Ok(BookingDto.From(b), "Seans devam ettirildi"));
        }

        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> Cancel(int id, [FromBody] BookingStatusChangeDto? dto)
        {
            var b = await _bookingService.ChangeStatus(id, BookingStatus.Canceled, BookingLogType.Cancel,
                dto?.MinutesAgo, dto?.Note, CurrentUserId());
            return Ok(ApiResponse.Ok(BookingDto.From(b), "Seans iptal edildi"));
        }

        // Oturum kapanışı (docs/wallet-redesign.md §4): kapsama (BÖL) → zaman tüketimi +
        // kapsanmayan süre için ücret. settlement: "PayNow" (peşin) | "Debt" (borca yaz).
        [HttpPost("{id}/close")]
        public async Task<IActionResult> Close(int id, BookingCloseDto dto)
        {
            var mode = Enum.TryParse<SettlementMode>(dto.Settlement, ignoreCase: true, out var m)
                ? m : SettlementMode.PayNow;
            var result = await _walletService.CloseBooking(id, mode, CurrentUserId(),
                dto.EndTime, dto.Note, dto.TableId, dto.ChildId, dto.ChargeAmount);
            return Ok(ApiResponse.Ok(result, "Oturum kapatıldı"));
        }
    }
}
