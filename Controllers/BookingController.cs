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

        // Oturum kapanışı (docs/wallet-redesign.md §4): kapsama (BÖL) → zaman tüketimi +
        // kapsanmayan süre için ücret. settlement: "PayNow" (peşin) | "Debt" (borca yaz).
        [HttpPost("{id}/close")]
        public async Task<IActionResult> Close(int id, BookingCloseDto dto)
        {
            var mode = Enum.TryParse<SettlementMode>(dto.Settlement, ignoreCase: true, out var m)
                ? m : SettlementMode.PayNow;
            var result = await _walletService.CloseBooking(id, mode, CurrentUserId(),
                dto.SubscriptionEndTime, dto.EndTime, dto.Note, dto.TableId, dto.ChildId, dto.ChargeAmount);
            return Ok(ApiResponse.Ok(result, "Oturum kapatıldı"));
        }
    }
}
