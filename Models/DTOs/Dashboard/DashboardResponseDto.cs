namespace Iroh.Models.DTOs.Dashboard
{
    public class DashboardResponseDto
    {
        public DashboardOverviewDto Overview { get; set; } = new();
        public List<TopCustomerDto> TopCustomers { get; set; } = new();
        public List<DailyBookingDto> DailyList { get; set; } = new();
        public List<RevenueChartDto> RevenueChart { get; set; } = new();
        public List<BusyHourDto> BusyHoursChart { get; set; } = new();
    }

    public class DashboardOverviewDto
    {
        // Client bunu "totalChildren" adıyla okuyor (eski fn_get_dashboard_overview.total_children = COUNT(DISTINCT booking) ile aynı değer).
        public int TotalChildren { get; set; }
        public int ActiveCurrently { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal BookingRevenue { get; set; }
        public decimal PurchaseRevenue { get; set; }
        public int AverageDurationMinutes { get; set; }
        public int CancelationRate { get; set; }
        public int PurchaseCount { get; set; }
        public int SubscriptionSessions { get; set; }
    }

    public class TopCustomerDto
    {
        public int? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int VisitCount { get; set; }
        public int PurchaseCount { get; set; }
        public decimal BookingSpent { get; set; }
        public decimal PurchaseSpent { get; set; }
        public decimal TotalSpent { get; set; }
    }

    public class DailyBookingDto
    {
        public int BookingId { get; set; }
        public string Name { get; set; } = string.Empty;
        // Değer bilerek boş (çocuk soyadı kullanılmıyor). Wire: camelCase "lastName".
        public string LastName { get; set; } = string.Empty;
        public string ParentName { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public bool IsSubscription { get; set; }
    }

    public class RevenueChartDto
    {
        public string Date { get; set; } = string.Empty;
        public decimal BookingRevenue { get; set; }
        public decimal PurchaseRevenue { get; set; }
    }

    public class BusyHourDto
    {
        public string Hour { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
