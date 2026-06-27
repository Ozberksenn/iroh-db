using Iroh.Models.Responses;
using Iroh.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Iroh.Controllers
{
    // /api/reports — gün-sonu Z-raporu + borçlular (alacak). Yalnızca okuma.
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        // GET /api/reports/z-report?date=...  (date verilmezse bugün — Türkiye günü)
        [HttpGet("z-report")]
        public async Task<IActionResult> ZReport([FromQuery] DateTimeOffset? date)
        {
            var istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
            var baseIst = TimeZoneInfo.ConvertTime(date ?? DateTimeOffset.UtcNow, istanbul);
            var dayStartIst = new DateTimeOffset(baseIst.Year, baseIst.Month, baseIst.Day, 0, 0, 0, baseIst.Offset);

            DateTime fromUtc = dayStartIst.UtcDateTime;
            DateTime toUtc = dayStartIst.AddDays(1).AddTicks(-1).UtcDateTime;

            var result = await _reportService.GetZReport(fromUtc, toUtc);
            return Ok(ApiResponse.Ok(result, "Z-raporu hazırlandı"));
        }

        // GET /api/reports/debtors
        [HttpGet("debtors")]
        public async Task<IActionResult> Debtors()
        {
            var result = await _reportService.GetDebtors();
            return Ok(ApiResponse.Ok(result, "Borçlular listesi"));
        }
    }
}
