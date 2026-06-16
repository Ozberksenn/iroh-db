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

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            // "bugün" Türkiye saatine göre (B5).
            var istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
            var todayIst = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, istanbul).Date;
            DateTime start = startDate ?? todayIst;
            DateTime end = endDate ?? todayIst.AddDays(1).AddTicks(-1);

            var result = await _dashboardService.GetDashboardSummary(start, end);
            return Ok(ApiResponse.Ok(result, "Dashboard verileri başarıyla getirildi"));
        }
    }
}
