using Iroh.Models.Responses;
using Iroh.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Iroh.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        // Tarih aralığı DateTimeOffset olarak alınır (client UTC "...Z" gönderir; offset korunur, belirsizlik yok).
        // Servise her zaman Kind=Utc DateTime geçilir (kolonlar UTC). "Bugün" Türkiye gününe göre hesaplanır.
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] DateTimeOffset? startDate, [FromQuery] DateTimeOffset? endDate)
        {
            var istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
            var nowIst = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, istanbul);
            var dayStartIst = new DateTimeOffset(nowIst.Year, nowIst.Month, nowIst.Day, 0, 0, 0, nowIst.Offset);

            DateTime startUtc = (startDate ?? dayStartIst).UtcDateTime;
            DateTime endUtc = (endDate ?? dayStartIst.AddDays(1).AddTicks(-1)).UtcDateTime;

            var result = await _dashboardService.GetDashboardSummary(startUtc, endUtc);
            return Ok(ApiResponse.Ok(result, "Dashboard verileri başarıyla getirildi"));
        }
    }
}
