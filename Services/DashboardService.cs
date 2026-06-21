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

            // Abonelik oturumu = cüzdandan SÜRE tüketmiş oturum (time_ledger Consumption).
            // Eski PurchaseBookings okuması migration sonrası yazılmadığı için bu metrik latent kırıktı; cüzdan düzeltir.
            var subscriptionSessions = await _context.TimeLedger
                .Where(e => e.Type == TimeLedgerType.Consumption && e.BookingId != null
                    && _context.Bookings.Any(b => b.Id == e.BookingId && b.StartTime >= startDate && b.StartTime <= endDate && (b.Child.Parent == null || !b.Child.Parent.IsDeleted)))
                .Select(e => e.BookingId)
                .Distinct()
                .CountAsync();

            // Kredi satışı geliri = cash_ledger Charge (booking'siz). Charge = satış değeri (peşin/borç fark etmez);
            // CloseBooking ücretleri booking_id taşır → hariç (onlar bookingRevenue'da). Σ(−amount_delta) = gelir.
            var purchaseRevenue = -(await _context.CashLedger
                .Where(cl => cl.Type == CashLedgerType.Charge && cl.BookingId == null && cl.CreatedAt >= startDate && cl.CreatedAt <= endDate)
                .SumAsync(cl => (decimal?)cl.AmountDelta) ?? 0m);

            response.Overview = new DashboardOverviewDto
            {
                TotalChildren = totalBookings,
                ActiveCurrently = activeCurrently,
                BookingRevenue = bookingRevenue,
                PurchaseRevenue = purchaseRevenue,
                TotalRevenue = bookingRevenue + purchaseRevenue,
                AverageDurationMinutes = (int)Math.Round(avgDuration),
                CancelationRate = totalBookings > 0 ? (int)Math.Round((double)canceledCount / totalBookings * 100) : 0,
                PurchaseCount = await _context.CashLedger.CountAsync(cl => cl.Type == CashLedgerType.Charge && cl.BookingId == null && cl.CreatedAt >= startDate && cl.CreatedAt <= endDate),
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
                    ParentName = (b.Child.Parent.Name ?? "Misafir") + " " + (b.Child.Parent.LastName ?? ""),
                    ParentId = b.Child.ParentId,
                    CheckIn = b.StartTime,
                    CheckOut = b.EndTime,
                    Status = b.Status.ToString(),
                    Price = b.Price,
                    IsSubscription = _context.TimeLedger.Any(e => e.BookingId == b.Id && e.Type == TimeLedgerType.Consumption)
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
                    // Kredi satışı = cash_ledger Charge (booking'siz); cüzdan üzerinden müşteriye bağlanır.
                    PurchaseCount = _context.CashLedger.Count(cl => cl.Type == CashLedgerType.Charge && cl.BookingId == null && cl.CreatedAt >= startDate && cl.CreatedAt <= endDate
                                    && _context.Wallets.Any(w => w.Id == cl.WalletId && w.CustomerId == c.Id)),
                    BookingSpent = _context.Bookings.Where(b => b.Child != null && b.Child.ParentId == c.Id && !b.Child.IsDeleted && b.StartTime >= startDate && b.StartTime <= endDate)
                                    .Sum(b => (decimal?)b.Price ?? 0),
                    PurchaseSpent = -(_context.CashLedger.Where(cl => cl.Type == CashLedgerType.Charge && cl.BookingId == null && cl.CreatedAt >= startDate && cl.CreatedAt <= endDate
                                    && _context.Wallets.Any(w => w.Id == cl.WalletId && w.CustomerId == c.Id))
                                    .Sum(cl => (decimal?)cl.AmountDelta) ?? 0m),
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

            // 4. Revenue Chart — kolonlar UTC; gün/saat bucket'ı Europe/Istanbul iş gününe göre.
            // Çevrim bellek içinde yapılır (DST-güvenli); dashboard aralığı küçük olduğundan maliyet düşük.
            var istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
            DateTime ToIst(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), istanbul);

            var dateList = new List<DateTime>();
            for (var dt = ToIst(startDate).Date; dt <= ToIst(endDate).Date; dt = dt.AddDays(1)) {
                dateList.Add(dt);
            }

            var bookingRevRows = await _context.Bookings
                .Where(b => b.StartTime >= startDate && b.StartTime <= endDate && b.StartTime != null)
                .Select(b => new { Start = b.StartTime!.Value, b.Price })
                .ToListAsync();
            var bookingRevs = bookingRevRows
                .GroupBy(b => ToIst(b.Start).Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(b => (decimal?)b.Price ?? 0) })
                .ToList();

            var purchaseRevRows = await _context.CashLedger
                .Where(cl => cl.Type == CashLedgerType.Charge && cl.BookingId == null && cl.CreatedAt >= startDate && cl.CreatedAt <= endDate)
                .Select(cl => new { cl.CreatedAt, cl.AmountDelta })
                .ToListAsync();
            var purchaseRevs = purchaseRevRows
                .GroupBy(cl => ToIst(cl.CreatedAt).Date)
                .Select(g => new { Date = g.Key, Total = -g.Sum(cl => cl.AmountDelta) })
                .ToList();

            response.RevenueChart = dateList.Select(d => new RevenueChartDto
            {
                Date = d.ToString("dd.MM"),
                BookingRevenue = bookingRevs.FirstOrDefault(r => r.Date == d)?.Total ?? 0,
                PurchaseRevenue = purchaseRevs.FirstOrDefault(r => r.Date == d)?.Total ?? 0
            }).ToList();

            // 5. Busy Hours — saat bucket'ı Europe/Istanbul'a göre (kolonlar UTC).
            var busyStarts = await _context.Bookings
                .Where(b => b.StartTime >= startDate && b.StartTime <= endDate && b.StartTime != null)
                .Select(b => b.StartTime!.Value)
                .ToListAsync();

            response.BusyHoursChart = busyStarts
                .GroupBy(s => ToIst(s).Hour)
                .OrderBy(g => g.Key)
                .Select(g => new BusyHourDto { Hour = g.Key.ToString("D2") + ":00", Count = g.Count() })
                .ToList();

            return response;
        }
    }
}
