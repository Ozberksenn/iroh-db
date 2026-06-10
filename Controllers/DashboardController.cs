using Iroh.Models.CustomResponses;
using Iroh.Models.DTOs.Dashboard;
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
        private readonly DashboardService _dashboardService;

        public DashboardController(DashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            // Eğer tarih gelmezse bugünü (Türkiye saatine göre) baz al
            DateTime start = startDate ?? DateTime.Today;
            DateTime end = endDate ?? DateTime.Today.AddDays(1).AddTicks(-1);

            var result = await _dashboardService.GetDashboardSummary(start, end);
            var response = new CustomResponse<DashboardResponseDto>(true, "Dashboard verileri başarıyla getirildi", result);
            return Ok(response);
        }
    }
}
