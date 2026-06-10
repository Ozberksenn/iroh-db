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

            // 1. Overview Stats
            var bookingsInRange = await _context.Booking
                .Where(b => b.startTime >= startDate && b.startTime <= endDate)
                .Include(b => b.child)
                .Include(b => b.customer)
                .ToListAsync();

            var purchasesInRange = await _context.Purchase
                .Where(p => p.createdAt >= startDate && p.createdAt <= endDate)
                .ToListAsync();

            int totalBookings = bookingsInRange.Count;
            int canceledCount = bookingsInRange.Count(b => b.status == BookingStatus.Canceled);
            int activeCurrently = await _context.Booking.CountAsync(b => b.status == BookingStatus.Active);
            
            decimal bookingRevenue = bookingsInRange
                .Where(b => b.status == BookingStatus.Completed && b.price.HasValue)
                .Sum(b => (decimal)b.price!.Value);

            decimal purchaseRevenue = purchasesInRange.Sum(p => (decimal)p.price);

            double avgDuration = 0;
            var completedBookings = bookingsInRange.Where(b => b.status == BookingStatus.Completed && b.startTime.HasValue && b.endTime.HasValue).ToList();
            if (completedBookings.Any())
            {
                avgDuration = completedBookings.Average(b => (b.endTime!.Value - b.startTime!.Value).TotalMinutes);
            }

            int subscriptionSessions = bookingsInRange.Count(b => b.subscriptionStartTime.HasValue);

            response.overview = new DashboardOverviewDto
            {
                totalChildren = totalBookings, // Node.js'teki logic'e göre toplam oturum/çocuk sayısı
                activeCurrently = activeCurrently,
                bookingRevenue = bookingRevenue,
                purchaseRevenue = purchaseRevenue,
                totalRevenue = bookingRevenue + purchaseRevenue,
                averageDurationMinutes = (int)Math.Round(avgDuration),
                cancelationRate = totalBookings > 0 ? (int)Math.Round((double)canceledCount / totalBookings * 100) : 0,
                purchaseCount = purchasesInRange.Count,
                subscriptionSessions = subscriptionSessions
            };

            // 2. Daily List
            response.dailyList = bookingsInRange.Select(b => new DailyBookingDto
            {
                bookingId = b.id,
                name = b.child?.name ?? "Bilinmiyor",
                lastname = b.customer?.lastName ?? "",
                parentname = b.customer?.name ?? "Bilinmiyor",
                parentId = b.customerId,
                checkIn = b.startTime,
                checkOut = b.endTime,
                status = b.status.ToString(),
                price = b.price,
                isSubscription = b.subscriptionStartTime.HasValue
            }).OrderByDescending(b => b.checkIn).ToList();

            // 3. Top Customers
            var topCustomers = await _context.Customer
                .Select(c => new TopCustomerDto
                {
                    id = c.id,
                    name = c.name + " " + (c.lastName ?? ""),
                    visitCount = _context.Booking.Count(b => b.customerId == c.id && b.startTime >= startDate && b.startTime <= endDate),
                    purchaseCount = _context.Purchase.Count(p => p.customerId == c.id && p.createdAt >= startDate && p.createdAt <= endDate),
                    bookingSpent = _context.Booking.Where(b => b.customerId == c.id && b.startTime >= startDate && b.startTime <= endDate && b.status == BookingStatus.Completed && b.price.HasValue)
                                    .Sum(b => (decimal)b.price!.Value),
                    purchaseSpent = _context.Purchase.Where(p => p.customerId == c.id && p.createdAt >= startDate && p.createdAt <= endDate)
                                    .Sum(p => (decimal)p.price),
                })
                .Where(tc => tc.visitCount > 0 || tc.purchaseCount > 0)
                .OrderByDescending(tc => tc.bookingSpent + tc.purchaseSpent)
                .Take(10)
                .ToListAsync();
            
            foreach(var tc in topCustomers) {
                tc.totalSpent = tc.bookingSpent + tc.purchaseSpent;
            }
            response.topCustomers = topCustomers;

            // 4. Revenue Chart (Son 7 gün veya aralık bazlı)
            // Basitlik için sadece startDate-endDate aralığını günlere bölüyoruz
            var days = (endDate - startDate).Days + 1;
            for (int i = 0; i < days; i++)
            {
                var date = startDate.AddDays(i).Date;
                var nextDate = date.AddDays(1);

                var bRev = bookingsInRange
                    .Where(b => b.startTime >= date && b.startTime < nextDate && b.status == BookingStatus.Completed && b.price.HasValue)
                    .Sum(b => (decimal)b.price!.Value);

                var pRev = purchasesInRange
                    .Where(p => p.createdAt >= date && p.createdAt < nextDate)
                    .Sum(p => (decimal)p.price);

                response.revenueChart.Add(new RevenueChartDto
                {
                    date = date.ToString("yyyy-MM-dd"),
                    bookingRevenue = bRev,
                    purchaseRevenue = pRev
                });
            }

            // 5. Busy Hours Chart
            var busyHours = bookingsInRange
                .Where(b => b.startTime.HasValue)
                .GroupBy(b => b.startTime!.Value.Hour)
                .Select(g => new BusyHourDto
                {
                    hour = $"{g.Key:D2}:00",
                    count = g.Count()
                })
                .OrderBy(g => g.hour)
                .ToList();
            response.busyHoursChart = busyHours;

            return response;
        }
    }
}
