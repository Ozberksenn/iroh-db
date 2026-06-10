namespace Iroh.Models.DTOs.Dashboard
{
    public class DashboardResponseDto
    {
        public DashboardOverviewDto overview { get; set; } = new();
        public List<TopCustomerDto> topCustomers { get; set; } = new();
        public List<DailyBookingDto> dailyList { get; set; } = new();
        public List<RevenueChartDto> revenueChart { get; set; } = new();
        public List<BusyHourDto> busyHoursChart { get; set; } = new();
    }

    public class DashboardOverviewDto
    {
        public int totalChildren { get; set; }
        public int activeCurrently { get; set; }
        public decimal totalRevenue { get; set; }
        public decimal bookingRevenue { get; set; }
        public decimal purchaseRevenue { get; set; }
        public int averageDurationMinutes { get; set; }
        public int cancelationRate { get; set; }
        public int purchaseCount { get; set; }
        public int subscriptionSessions { get; set; }
    }

    public class TopCustomerDto
    {
        public int? id { get; set; }
        public string name { get; set; } = string.Empty;
        public int visitCount { get; set; }
        public int purchaseCount { get; set; }
        public decimal bookingSpent { get; set; }
        public decimal purchaseSpent { get; set; }
        public decimal totalSpent { get; set; }
    }

    public class DailyBookingDto
    {
        public int bookingId { get; set; }
        public string name { get; set; } = string.Empty;
        public string lastname { get; set; } = string.Empty;
        public string parentname { get; set; } = string.Empty;
        public int? parentId { get; set; }
        public DateTime? checkIn { get; set; }
        public DateTime? checkOut { get; set; }
        public string status { get; set; } = string.Empty;
        public int? price { get; set; }
        public bool isSubscription { get; set; }
    }

    public class RevenueChartDto
    {
        public string date { get; set; } = string.Empty;
        public decimal bookingRevenue { get; set; }
        public decimal purchaseRevenue { get; set; }
    }

    public class BusyHourDto
    {
        public string hour { get; set; } = string.Empty;
        public int count { get; set; }
    }
}
