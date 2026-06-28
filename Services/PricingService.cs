using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    // Abonelik dışı (kapsanmayan) sürenin ₺ ücretini hesaplar.
    // Cüzdan oturum-kapanışı bunu "kapsanmayan dakika" için çağırır.
    public interface IPricingService
    {
        Task<decimal> PriceForMinutes(int minutes);
    }

    // "İlk saat + sonraki her yarım saat" modeli; oranlar Company kaydından (tek tenant).
    // Client'taki firstHour / additionalHalfHour ile aynı kaynak — tek doğruluk noktası.
    public class PricingService : IPricingService
    {
        private readonly AppDbContext _context;

        public PricingService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<decimal> PriceForMinutes(int minutes)
        {
            if (minutes <= 0) return 0m;

            var company = await _context.Companies.AsNoTracking().FirstOrDefaultAsync();
            decimal firstHour = company?.FirstHourPrice ?? 0;
            decimal additionalHalfHour = company?.AdditionalHalfHourPrice ?? 0;

            var price = firstHour;
            var remaining = minutes - 60;
            if (remaining > 0)
            {
                var halfHours = (int)Math.Ceiling(remaining / 30.0);
                price += halfHours * additionalHalfHour;
            }
            return price;
        }
    }
}
