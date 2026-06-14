using Iroh.Models.DTOs.Dashboard;
using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public interface IDashboardService
    {
        Task<DashboardResponseDto> GetDashboardSummary(DateTime startDate, DateTime endDate);
    }

    public class DashboardService : IDashboardService
    {
        private readonly AppDbContext _context;

        public DashboardService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardResponseDto> GetDashboardSummary(DateTime startDate, DateTime endDate)
        {
            var response = new DashboardResponseDto();

            // 1. Overview Stats (fn_get_dashboard_overview)
            var totalBookings = await _context.Bookings
                .CountAsync(b => b.StartTime >= startDate && b.StartTime <= endDate && (b.Child.Parent == null || !b.Child.Parent.IsDeleted));

            var activeCurrently = await _context.Bookings
                .CountAsync(b => b.StartTime >= startDate && b.StartTime <= endDate && (b.Status == BookingStatus.Active) && (b.Child.Parent == null || !b.Child.Parent.IsDeleted));

            var canceledCount = await _context.Bookings
                .CountAsync(b => b.StartTime >= startDate && b.StartTime <= endDate && (b.Status == BookingStatus.Canceled) && (b.Child.Parent == null || !b.Child.Parent.IsDeleted));

            var bookingRevenue = await _context.Bookings
                .Where(b => b.StartTime >= startDate && b.StartTime <= endDate && (b.Child.Parent == null || !b.Child.Parent.IsDeleted))
                .SumAsync(b => (decimal?)b.Price ?? 0);

            var avgDuration = 0.0;
            var durationQuery = _context.Bookings
                .Where(b => b.StartTime >= startDate && b.StartTime <= endDate && b.EndTime != null && b.Status != BookingStatus.Canceled && (b.Child.Parent == null || !b.Child.Parent.IsDeleted));
            
            if (await durationQuery.AnyAsync())
            {
                avgDuration = await durationQuery.AverageAsync(b => (b.EndTime!.Value - b.StartTime!.Value).TotalMinutes);
            }

            var subscriptionSessions = await _context.PurchaseBookings
                .Where(pb => _context.Bookings.Any(b => b.Id == pb.BookingId && b.StartTime >= startDate && b.StartTime <= endDate && (b.Child.Parent == null || !b.Child.Parent.IsDeleted)))
                .Select(pb => pb.BookingId)
                .Distinct()
                .CountAsync();

            var purchaseRevenue = await _context.Purchases
                .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate)
                .SumAsync(p => (decimal)p.Price);

            response.Overview = new DashboardOverviewDto
            {
                TotalBookings = totalBookings,
                ActiveCurrently = activeCurrently,
                BookingRevenue = bookingRevenue,
                PurchaseRevenue = purchaseRevenue,
                TotalRevenue = bookingRevenue + purchaseRevenue,
                AverageDurationMinutes = (int)Math.Round(avgDuration),
                CancelationRate = totalBookings > 0 ? (int)Math.Round((double)canceledCount / totalBookings * 100) : 0,
                PurchaseCount = await _context.Purchases.CountAsync(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate),
                SubscriptionSessions = subscriptionSessions
            };

            // 2. Daily List (fn_get_dashboard_daily_list)
            response.DailyList = await _context.Bookings
                .Where(b => b.StartTime >= startDate && b.StartTime <= endDate && (b.Child.Parent == null || !b.Child.Parent.IsDeleted))
                .OrderByDescending(b => b.StartTime)
                .Take(10)
                .Select(b => new DailyBookingDto
                {
                    BookingId = b.Id,
                    Name = b.Child.Name ?? "Bilinmeyen",
                    LastName = "",
                    Parentname = (b.Child.Parent.Name ?? "Misafir") + " " + (b.Child.Parent.LastName ?? ""),
                    ParentId = b.Child.ParentId,
                    CheckIn = b.StartTime,
                    CheckOut = b.EndTime,
                    Status = b.Status.ToString(),
                    Price = b.Price,
                    IsSubscription = _context.PurchaseBookings.Any(pb => pb.BookingId == b.Id)
                })
                .ToListAsync();

            // 3. Top Customers (fn_get_dashboard_top_customers)
            var topCustomers = await _context.Customers
                .Where(c => !c.IsDeleted)
                .Select(c => new TopCustomerDto
                {
                    Id = c.Id,
                    Name = c.Name + " " + (c.LastName ?? ""),
                    // proc: child silinmemiş + aralıkta TÜM bookings (sadece Completed değil!).
                    VisitCount = _context.Bookings.Count(b => b.Child != null && b.Child.ParentId == c.Id && !b.Child.IsDeleted && b.StartTime >= startDate && b.StartTime <= endDate),
                    PurchaseCount = _context.Purchases.Count(p => p.CustomerId == c.Id && p.CreatedAt >= startDate && p.CreatedAt <= endDate),
                    BookingSpent = _context.Bookings.Where(b => b.Child != null && b.Child.ParentId == c.Id && !b.Child.IsDeleted && b.StartTime >= startDate && b.StartTime <= endDate)
                                    .Sum(b => (decimal?)b.Price ?? 0),
                    PurchaseSpent = _context.Purchases.Where(p => p.CustomerId == c.Id && p.CreatedAt >= startDate && p.CreatedAt <= endDate)
                                    .Sum(p => (decimal)p.Price),
                })
                .Where(tc => tc.VisitCount > 0 || tc.PurchaseCount > 0)
                .OrderByDescending(tc => tc.BookingSpent + tc.PurchaseSpent)
                .Take(10)
                .ToListAsync();

            foreach (var tc in topCustomers)
            {
                tc.Name = tc.Name.Trim();
                tc.TotalSpent = tc.BookingSpent + tc.PurchaseSpent;
            }
            response.TopCustomers = topCustomers;

            // 4. Revenue Chart (fn_get_dashboard_revenue_chart)
            var dateList = new List<DateTime>();
            for (var dt = startDate.Date; dt <= endDate.Date; dt = dt.AddDays(1)) {
                dateList.Add(dt);
            }

            var bookingRevs = await _context.Bookings
                .Where(b => b.StartTime >= startDate && b.StartTime <= endDate)
                .GroupBy(b => b.StartTime!.Value.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(b => (decimal?)b.Price ?? 0) })
                .ToListAsync();

            var purchaseRevs = await _context.Purchases
                .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate)
                .GroupBy(p => p.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(p => (decimal)p.Price) })
                .ToListAsync();

            response.RevenueChart = dateList.Select(d => new RevenueChartDto
            {
                Date = d.ToString("dd.MM"),
                BookingRevenue = bookingRevs.FirstOrDefault(r => r.Date == d)?.Total ?? 0,
                PurchaseRevenue = purchaseRevs.FirstOrDefault(r => r.Date == d)?.Total ?? 0
            }).ToList();

            // 5. Busy Hours (fn_get_dashboard_busy_hours) — saat bucket'ı SQL'de (Istanbul TZ), format/sıra bellekte.
            var busyRaw = await _context.Bookings
                .Where(b => b.StartTime >= startDate && b.StartTime <= endDate)
                .GroupBy(b => b.StartTime!.Value.Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .ToListAsync();

            response.BusyHoursChart = busyRaw
                .OrderBy(x => x.Hour)
                .Select(x => new BusyHourDto { Hour = x.Hour.ToString("D2") + ":00", Count = x.Count })
                .ToList();

            return response;
        }
    }
}
