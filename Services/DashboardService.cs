using Iroh.Models.DTOs.Dashboard;
using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    public class DashboardService
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
            var totalBookings = await _context.Booking
                .CountAsync(b => b.startTime >= startDate && b.startTime <= endDate && (b.child.parent == null || !b.child.parent.isDeleted));

            var activeCurrently = await _context.Booking
                .CountAsync(b => b.startTime >= startDate && b.startTime <= endDate && (b.status == BookingStatus.Active) && (b.child.parent == null || !b.child.parent.isDeleted));

            var canceledCount = await _context.Booking
                .CountAsync(b => b.startTime >= startDate && b.startTime <= endDate && (b.status == BookingStatus.Canceled) && (b.child.parent == null || !b.child.parent.isDeleted));

            var bookingRevenue = await _context.Booking
                .Where(b => b.startTime >= startDate && b.startTime <= endDate && (b.child.parent == null || !b.child.parent.isDeleted))
                .SumAsync(b => (decimal?)b.price ?? 0);

            var avgDuration = 0.0;
            var durationQuery = _context.Booking
                .Where(b => b.startTime >= startDate && b.startTime <= endDate && b.endTime != null && b.status != BookingStatus.Canceled && (b.child.parent == null || !b.child.parent.isDeleted));
            
            if (await durationQuery.AnyAsync())
            {
                avgDuration = await durationQuery.AverageAsync(b => (b.endTime!.Value - b.startTime!.Value).TotalMinutes);
            }

            var subscriptionSessions = await _context.purchaseBookings
                .Where(pb => _context.Booking.Any(b => b.id == pb.bookingId && b.startTime >= startDate && b.startTime <= endDate && (b.child.parent == null || !b.child.parent.isDeleted)))
                .Select(pb => pb.bookingId)
                .Distinct()
                .CountAsync();

            var purchaseRevenue = await _context.Purchase
                .Where(p => p.createdAt >= startDate && p.createdAt <= endDate)
                .SumAsync(p => (decimal)p.price);

            response.overview = new DashboardOverviewDto
            {
                totalBookings = totalBookings,
                activeCurrently = activeCurrently,
                bookingRevenue = bookingRevenue,
                purchaseRevenue = purchaseRevenue,
                totalRevenue = bookingRevenue + purchaseRevenue,
                averageDurationMinutes = (int)Math.Round(avgDuration),
                cancelationRate = totalBookings > 0 ? (int)Math.Round((double)canceledCount / totalBookings * 100) : 0,
                purchaseCount = await _context.Purchase.CountAsync(p => p.createdAt >= startDate && p.createdAt <= endDate),
                subscriptionSessions = subscriptionSessions
            };

            // 2. Daily List (fn_get_dashboard_daily_list)
            response.dailyList = await _context.Booking
                .Where(b => b.startTime >= startDate && b.startTime <= endDate && (b.child.parent == null || !b.child.parent.isDeleted))
                .OrderByDescending(b => b.startTime)
                .Take(10)
                .Select(b => new DailyBookingDto
                {
                    bookingId = b.id,
                    name = b.child.name ?? "Bilinmeyen",
                    lastname = "",
                    parentname = (b.child.parent.name ?? "Misafir") + " " + (b.child.parent.lastName ?? ""),
                    parentId = b.child.parentId,
                    checkIn = b.startTime,
                    checkOut = b.endTime,
                    status = b.status.ToString(),
                    price = b.price,
                    isSubscription = _context.purchaseBookings.Any(pb => pb.bookingId == b.id)
                })
                .ToListAsync();

            // 3. Top Customers (fn_get_dashboard_top_customers)
            var topCustomers = await _context.Customer
                .Where(c => !c.isDeleted)
                .Select(c => new TopCustomerDto
                {
                    id = c.id,
                    name = c.name + " " + (c.lastName ?? ""),
                    // proc: child silinmemiş + aralıkta TÜM bookings (sadece Completed değil!).
                    visitCount = _context.Booking.Count(b => b.child != null && b.child.parentId == c.id && !b.child.isDeleted && b.startTime >= startDate && b.startTime <= endDate),
                    purchaseCount = _context.Purchase.Count(p => p.customerId == c.id && p.createdAt >= startDate && p.createdAt <= endDate),
                    bookingSpent = _context.Booking.Where(b => b.child != null && b.child.parentId == c.id && !b.child.isDeleted && b.startTime >= startDate && b.startTime <= endDate)
                                    .Sum(b => (decimal?)b.price ?? 0),
                    purchaseSpent = _context.Purchase.Where(p => p.customerId == c.id && p.createdAt >= startDate && p.createdAt <= endDate)
                                    .Sum(p => (decimal)p.price),
                })
                .Where(tc => tc.visitCount > 0 || tc.purchaseCount > 0)
                .OrderByDescending(tc => tc.bookingSpent + tc.purchaseSpent)
                .Take(10)
                .ToListAsync();

            foreach (var tc in topCustomers)
            {
                tc.name = tc.name.Trim();
                tc.totalSpent = tc.bookingSpent + tc.purchaseSpent;
            }
            response.topCustomers = topCustomers;

            // 4. Revenue Chart (fn_get_dashboard_revenue_chart)
            var dateList = new List<DateTime>();
            for (var dt = startDate.Date; dt <= endDate.Date; dt = dt.AddDays(1)) {
                dateList.Add(dt);
            }

            var bookingRevs = await _context.Booking
                .Where(b => b.startTime >= startDate && b.startTime <= endDate)
                .GroupBy(b => b.startTime!.Value.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(b => (decimal?)b.price ?? 0) })
                .ToListAsync();

            var purchaseRevs = await _context.Purchase
                .Where(p => p.createdAt >= startDate && p.createdAt <= endDate)
                .GroupBy(p => p.createdAt.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(p => (decimal)p.price) })
                .ToListAsync();

            response.revenueChart = dateList.Select(d => new RevenueChartDto
            {
                date = d.ToString("dd.MM"),
                bookingRevenue = bookingRevs.FirstOrDefault(r => r.Date == d)?.Total ?? 0,
                purchaseRevenue = purchaseRevs.FirstOrDefault(r => r.Date == d)?.Total ?? 0
            }).ToList();

            // 5. Busy Hours (fn_get_dashboard_busy_hours) — saat bucket'ı SQL'de (Istanbul TZ), format/sıra bellekte.
            var busyRaw = await _context.Booking
                .Where(b => b.startTime >= startDate && b.startTime <= endDate)
                .GroupBy(b => b.startTime!.Value.Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .ToListAsync();

            response.busyHoursChart = busyRaw
                .OrderBy(x => x.Hour)
                .Select(x => new BusyHourDto { hour = x.Hour.ToString("D2") + ":00", count = x.Count })
                .ToList();

            return response;
        }
    }
}
