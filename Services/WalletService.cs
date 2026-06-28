using Iroh.Domain;
using Iroh.Exceptions;
using Iroh.Models.DTOs.Wallet;
using Iroh.Models.Entities;
using Iroh.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Iroh.Services
{
    // Cüzdan + iki defter çekirdeği (docs/wallet-redesign.md).
    // RC3 bütünlük garantileri:
    //  • Her mutasyon transaction içinde (relational); kısmi yazım imkânsız.
    //  • Cüzdan satırı FOR UPDATE ile kilitlenir → eşzamanlı kardeş-seans kapanışı sıraya girer (çift-harcama yok).
    //  • Bakiyeler elle (+=) değil, append-only defterlerden YENİDEN hesaplanır (RecomputeBalances) → cache asla sapmaz.
    //  • Idempotency hem ön-kontrol hem ux_time_consumption_booking unique index ihlali yakalanarak garanti edilir.
    // (InMemory testlerde transaction/FOR UPDATE yoktur; IsRelational() guard'ı ile atlanır.)
    public interface IWalletService
    {
        Task<WalletDto> GetWallet(int customerId);

        // Abonelik / topup: + dakika. money > 0 ise peşin satış (Charge+Payment, net borç 0) kaydedilir.
        Task<WalletDto> CreditTime(int customerId, int minutes, decimal money, int? packageId, int? userId,
                                   DateTime? validFrom = null, DateTime? validTo = null);

        // Oturum kapanışı: kapsama hesabı (BÖL) → zaman tüketimi + kapsanmayan süre için ücret/borç.
        // Debt → aşım SÜRE borcu (DebtCharge); PayNow → chargeAmount (operatör tutarı, verilmezse Company oranı).
        // Tamamlama tek uç: booking alanlarını (endTime/not/masa/çocuk) da finalize eder.
        Task<CloseBookingResultDto> CloseBooking(int bookingId, SettlementMode settlement, int? userId,
            DateTime? endTime = null, string? note = null, int? tableId = null, int? childId = null,
            decimal? chargeAmount = null);

        // Borç ödeme: tüm (süre) borcu kapanır (kısmi yok); operatör para karşılığını girer.
        Task<WalletDto> SettleTimeDebt(int customerId, decimal amount, int? userId);

        Task<WalletDto> AdjustTime(int customerId, int minutesDelta, string reason, int? userId);
        Task<WalletDto> AdjustCash(int customerId, decimal amountDelta, string reason, int? userId);
    }

    public class WalletService : IWalletService
    {
        private readonly AppDbContext _context;
        private readonly IPricingService _pricing;

        public WalletService(AppDbContext context, IPricingService pricing)
        {
            _context = context;
            _pricing = pricing;
        }

        // ---- statü türetimi: Aktif = geçerli pencere VE kullanılabilir bakiye. Geçerli ama bakiye 0 (tükenmiş)
        // → Pasif (Subscriber), süresi dolmuşla aynı kovada. Kalan dakika/borç ayrı sinyaller. ----
        public static SubscriptionStatus Derive(bool validNow, bool hasBalance, bool hasUpcoming, bool hasAny) =>
            validNow && hasBalance ? SubscriptionStatus.ActiveSubscriber
          : validNow              ? SubscriptionStatus.Subscriber          // geçerli ama bakiye bitmiş → pasif
          : hasUpcoming           ? SubscriptionStatus.UpcomingSubscriber
          : hasAny                ? SubscriptionStatus.Subscriber           // süresi dolmuş → pasif
          :                         SubscriptionStatus.Customer;

        // ---- kova durumu (Aşama B, docs/subscription-buckets.md): time_ledger satırlarından SAF türetim ----
        public readonly record struct BucketState(
            int AvailableNow, bool HasValidNow, bool HasUpcoming, bool HasAny,
            DateTime? NextExpiry, DateTime? AggValidFrom, DateTime? AggValidTo, int Burned);

        private sealed class Bucket { public DateTime? Vf; public DateTime? Vt; public int Remaining; }

        private static bool BucketValidAt(Bucket b, DateTime t) =>
            (!b.Vf.HasValue || b.Vf.Value <= t) && (!b.Vt.HasValue || b.Vt.Value >= t);

        // Kronolojik yeniden-oynatma: her Consumption o anda geçerli kovalardan önce-süresi-dolacak sırayla düşülür;
        // kovalar yetmezse süresiz "havuz"dan (Correction/Refund). Süresi dolan kovanın kalanı yanar (AvailableNow'a girmez).
        public static BucketState ComputeBuckets(IReadOnlyList<TimeLedgerEntry> rows, DateTime now)
        {
            var ordered = rows
                .Where(r => r.Type == TimeLedgerType.Credit || r.Type == TimeLedgerType.Consumption
                         || r.Type == TimeLedgerType.Correction || r.Type == TimeLedgerType.Refund)
                .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToList();

            var buckets = new List<Bucket>();
            var pool = 0;
            var hasAny = false;

            foreach (var r in ordered)
            {
                switch (r.Type)
                {
                    case TimeLedgerType.Credit:
                        hasAny = true;
                        buckets.Add(new Bucket { Vf = r.ValidFrom, Vt = r.ValidTo, Remaining = r.MinutesDelta });
                        break;
                    case TimeLedgerType.Consumption:
                        var need = -r.MinutesDelta;   // tüketim negatif → pozitif ihtiyaç
                        foreach (var b in buckets
                            .Where(x => x.Remaining > 0 && BucketValidAt(x, r.CreatedAt))
                            .OrderBy(x => x.Vt ?? DateTime.MaxValue).ThenBy(x => x.Vf ?? DateTime.MinValue))
                        {
                            if (need <= 0) break;
                            var take = Math.Min(need, b.Remaining);
                            b.Remaining -= take;
                            need -= take;
                        }
                        if (need > 0) pool -= need;   // kovalar yetmedi → süresiz havuzdan
                        break;
                    case TimeLedgerType.Correction:
                    case TimeLedgerType.Refund:
                        pool += r.MinutesDelta;       // süresiz havuz (manuel ayar → yanmaz)
                        break;
                }
            }

            var live = buckets.Where(b => !b.Vt.HasValue || b.Vt.Value >= now).ToList();
            return new BucketState(
                AvailableNow: buckets.Where(b => BucketValidAt(b, now)).Sum(b => b.Remaining) + pool,
                HasValidNow: buckets.Any(b => BucketValidAt(b, now)),
                HasUpcoming: buckets.Any(b => b.Vf.HasValue && b.Vf.Value > now),
                HasAny: hasAny,
                NextExpiry: buckets.Where(b => b.Remaining > 0 && BucketValidAt(b, now) && b.Vt.HasValue)
                    .Select(b => (DateTime?)b.Vt!.Value).OrderBy(d => d).FirstOrDefault(),
                AggValidFrom: live.Where(b => b.Vf.HasValue).Select(b => (DateTime?)b.Vf!.Value).OrderBy(d => d).FirstOrDefault(),
                AggValidTo: live.Where(b => b.Vt.HasValue).Select(b => (DateTime?)b.Vt!.Value).OrderByDescending(d => d).FirstOrDefault(),
                Burned: buckets.Where(b => b.Vt.HasValue && b.Vt.Value < now).Sum(b => b.Remaining));
        }

        // ---- RC3 altyapısı ----

        // Mutasyonu transaction içinde çalıştırır. InMemory (testler) transaction desteklemez → doğrudan koşar.
        private async Task<T> InTransaction<T>(Func<Task<T>> action)
        {
            if (!_context.Database.IsRelational())
                return await action();

            await using var tx = await _context.Database.BeginTransactionAsync();
            var result = await action();
            await tx.CommitAsync();
            return result;
        }

        // Materyalize bakiyeleri (TimeBalanceMinutes / CashBalance) defterlerden YENİDEN hesaplar — ledger otorite.
        // Elle += yerine çağrılır; çağrıdan önce ilgili defter satırları SaveChanges'lenmiş olmalı.
        // Süre-borcu (DebtCharge/DebtSettle) defteri balance'a karışmaz; ayrı toplanır.
        private static readonly TimeLedgerType[] DebtTypes =
            { TimeLedgerType.DebtCharge, TimeLedgerType.DebtSettle };

        private async Task RecomputeBalances(Wallet wallet)
        {
            var now = DateTime.UtcNow;
            var timeRows = await _context.TimeLedger
                .Where(e => e.WalletId == wallet.Id).ToListAsync();

            // Aşama B: bakiye = ŞU AN geçerli kovaların kalanı + havuz; pencere kovalardan toplulaştırılır.
            var bs = ComputeBuckets(timeRows, now);
            wallet.TimeBalanceMinutes = bs.AvailableNow;
            wallet.ValidFrom = bs.AggValidFrom;
            wallet.ValidTo = bs.AggValidTo;

            // Süre-borcu: DebtCharge(+) − DebtSettle(−) → kalan borçlu dakika (≥0).
            wallet.TimeDebtMinutes = timeRows.Where(e => DebtTypes.Contains(e.Type)).Sum(e => e.MinutesDelta);

            wallet.CashBalance = await _context.CashLedger
                .Where(e => e.WalletId == wallet.Id).SumAsync(e => (decimal?)e.AmountDelta) ?? 0m;
            wallet.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        // Postgres unique-violation (ux_time_consumption_booking) → eşzamanlı çift-kapanış yakalama.
        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";

        // forUpdate (relational): cüzdan satırını kilitle → aynı cüzdana eşzamanlı yazımlar sıraya girer.
        private async Task<Wallet> GetOrCreateWallet(int customerId, bool forUpdate = false)
        {
            if (customerId == SystemConstants.GuestCustomerId)
                throw new BusinessRuleException("Sistem Misafiri kaydına cüzdan tanımlanamaz!");

            Wallet? wallet;
            if (forUpdate && _context.Database.IsRelational())
            {
                // ToListAsync (terminal) → EF FromSql'i alt-sorguya SARMAZ; Postgres alt-sorguda FOR UPDATE'e izin vermez.
                // customer_id UNIQUE olduğundan en fazla 1 satır döner.
                var locked = await _context.Wallets
                    .FromSqlInterpolated($"SELECT * FROM public.wallets WHERE customer_id = {customerId} FOR UPDATE")
                    .ToListAsync();
                wallet = locked.FirstOrDefault();
            }
            else
            {
                wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.CustomerId == customerId);
            }

            if (wallet == null)
            {
                wallet = new Wallet { CustomerId = customerId, UpdatedAt = DateTime.UtcNow };
                _context.Wallets.Add(wallet);
                await _context.SaveChangesAsync();
            }
            return wallet;
        }

        public async Task<WalletDto> GetWallet(int customerId)
        {
            var wallet = await _context.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.CustomerId == customerId);
            if (wallet == null)
            {
                // Cüzdanı olmayan müşteri = düz "Customer".
                return new WalletDto { CustomerId = customerId, Status = nameof(SubscriptionStatus.Customer) };
            }

            var timeRows = await _context.TimeLedger.AsNoTracking()
                .Where(e => e.WalletId == wallet.Id).OrderByDescending(e => e.Id).ToListAsync();
            var cashRows = await _context.CashLedger.AsNoTracking()
                .Where(e => e.WalletId == wallet.Id).OrderByDescending(e => e.Id).ToListAsync();

            var now = DateTime.UtcNow;
            var bs = ComputeBuckets(timeRows, now);
            var status = Derive(bs.HasValidNow, bs.AvailableNow > 0, bs.HasUpcoming, bs.HasAny);

            return new WalletDto
            {
                Id = wallet.Id,
                CustomerId = wallet.CustomerId,
                TimeBalanceMinutes = bs.AvailableNow,
                TimeDebtMinutes = wallet.TimeDebtMinutes,
                CashBalance = wallet.CashBalance,
                ValidFrom = bs.AggValidFrom,
                ValidTo = bs.AggValidTo,
                NextExpiry = bs.NextExpiry,
                BurnedMinutes = bs.Burned,
                Status = status.ToString(),
                TimeLedger = timeRows.Select(TimeLedgerEntryDto.From).ToList(),
                CashLedger = cashRows.Select(CashLedgerEntryDto.From).ToList()
            };
        }

        public async Task<WalletDto> CreditTime(int customerId, int minutes, decimal money, int? packageId, int? userId,
                                                DateTime? validFrom = null, DateTime? validTo = null)
        {
            if (minutes <= 0)
                throw new BusinessRuleException("Eklenecek süre pozitif olmalıdır.");
            if (money < 0m)
                throw new BusinessRuleException("Ücret negatif olamaz.");

            return await InTransaction(async () =>
            {
                var wallet = await GetOrCreateWallet(customerId, forUpdate: true);

                // Borç netleme (kullanıcı kararı): borçluysa yeni kredinin dakikaları ÖNCE süre-borcunu kapatır,
                // kalanı kullanılabilir bakiyeye yazılır. (Örn 5sa borç + 10sa kredi → 5sa borç kapanır, 5sa kalır.)
                var toDebt = Math.Min(minutes, wallet.TimeDebtMinutes);
                if (toDebt > 0)
                {
                    _context.TimeLedger.Add(new TimeLedgerEntry
                    {
                        WalletId = wallet.Id,
                        Type = TimeLedgerType.DebtSettle,
                        MinutesDelta = -toDebt,
                        PackageId = packageId,
                        UserId = userId,
                        Reason = "Kredi ile süre borcu netleme",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                var toBalance = minutes - toDebt;
                if (toBalance > 0)
                {
                    _context.TimeLedger.Add(new TimeLedgerEntry
                    {
                        WalletId = wallet.Id,
                        Type = TimeLedgerType.Credit,
                        MinutesDelta = toBalance,
                        PackageId = packageId,
                        UserId = userId,
                        ValidFrom = validFrom,   // Aşama B: kova kendi penceresini taşır
                        ValidTo = validTo,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // Aşama B: geçerlilik artık Credit kovasında taşınır (yukarıda); wallet penceresi RecomputeBalances'ta türetilir.

                // Peşin satış: net borç etkisi 0 (Charge − money + Payment + money), para izi kalır.
                if (money > 0m)
                {
                    _context.CashLedger.Add(new CashLedgerEntry
                    {
                        WalletId = wallet.Id, Type = CashLedgerType.Charge, AmountDelta = -money,
                        Reason = "Abonelik/kredi satışı", UserId = userId, CreatedAt = DateTime.UtcNow
                    });
                    _context.CashLedger.Add(new CashLedgerEntry
                    {
                        WalletId = wallet.Id, Type = CashLedgerType.Payment, AmountDelta = money,
                        Reason = "Abonelik/kredi tahsilatı", UserId = userId, CreatedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();   // defter satırları + geçerlilik penceresi
                await RecomputeBalances(wallet);      // bakiyeleri defterden türet (elle += yok)
                return await GetWallet(customerId);
            });
        }

        public async Task<CloseBookingResultDto> CloseBooking(int bookingId, SettlementMode settlement, int? userId,
            DateTime? endTime = null, string? note = null, int? tableId = null, int? childId = null,
            decimal? chargeAmount = null)
        {
            return await InTransaction(async () =>
            {
                var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
                if (booking == null)
                    throw new NotFoundException("Oturum bulunamadı!");

                var now = DateTime.UtcNow;
                // Kapanışta düzenlenebilir alanlar (operatör masa/çocuk/not değiştirebilir).
                if (tableId.HasValue) booking.TableId = tableId;
                if (childId.HasValue) booking.ChildId = childId;
                if (note != null) booking.Note = note;
                // Finalize zaman penceresi: client TAM (faturalanan) süreyi endTime ile gönderir; kapsama BÖL'ünü servis yapar.
                // dur = faturalanan pencere (EndTime − StartTime). subscription* alanları kaldırıldı; kapsama ayrımı LEDGER'da.
                if (!booking.StartTime.HasValue)
                    booking.StartTime = now;
                booking.EndTime = endTime ?? booking.EndTime ?? now;

                var dur = (int)Math.Round((booking.EndTime!.Value - booking.StartTime!.Value).TotalMinutes);
                if (dur < 0) dur = 0;

                // Parent (cüzdan sahibi) çocuk üzerinden bulunur.
                // Yalnızca ParentId projekte edilir — Child entity'sini tam yüklemek birth_date NULL'da patlıyor.
                int? parentId = null;
                if (booking.ChildId.HasValue)
                {
                    parentId = await _context.Children.AsNoTracking()
                        .Where(c => c.Id == booking.ChildId.Value)
                        .Select(c => (int?)c.ParentId)
                        .FirstOrDefaultAsync();
                }

                // Misafir / parent yok → cüzdan/tab yok; ücret yalnızca booking.Price'a yazılır (nakit, eski akış).
                if (parentId == null || parentId.Value == SystemConstants.GuestCustomerId)
                {
                    var guestPrice = await _pricing.PriceForMinutes(dur);
                    booking.Price = guestPrice;
                    booking.Status = BookingStatus.Completed;
                    await _context.SaveChangesAsync();
                    return new CloseBookingResultDto { BookingId = bookingId, DurationMinutes = dur, HasWallet = false, UncoveredMinutes = dur, Charged = guestPrice };
                }

                // Cüzdanı KİLİTLE: idempotency kontrolü + kapsama kararı + yazım hep bu kilit altında (TOCTOU yok).
                var wallet = await GetOrCreateWallet(parentId.Value, forUpdate: true);
                var result = new CloseBookingResultDto { BookingId = bookingId, DurationMinutes = dur, HasWallet = true };

                // Idempotent ön-kontrol (kilit altında): booking zaten Completed VEYA tüketim kaydı varsa tekrar işleme.
                // Status kontrolü kapsanmayan/expired kapanışları da korur — onlarda Consumption satırı olmaz,
                // yoksa tekrar kapatma çift ücret yazardı.
                var alreadyProcessed = booking.Status == BookingStatus.Completed
                    || await _context.TimeLedger.AnyAsync(e => e.BookingId == bookingId
                        && (e.Type == TimeLedgerType.Consumption || e.Type == TimeLedgerType.DebtCharge));
                if (alreadyProcessed)
                {
                    booking.Status = BookingStatus.Completed;
                    await _context.SaveChangesAsync();
                    result.AlreadyProcessed = true;
                    result.WalletAfter = await GetWallet(parentId.Value);
                    return result;
                }

                // Aşama B: kapsama, ŞU AN geçerli kovaların kalanından (önce-süresi-dolacak). Tek Consumption satırı
                // yazılır; hangi kovadan düştüğü RecomputeBalances/ComputeBuckets ile deterministik çözülür.
                var bucketRows = await _context.TimeLedger.Where(e => e.WalletId == wallet.Id).ToListAsync();
                var available = ComputeBuckets(bucketRows, now).AvailableNow;
                var cover = Math.Min(dur, Math.Max(0, available));

                if (cover > 0)
                {
                    _context.TimeLedger.Add(new TimeLedgerEntry
                    {
                        WalletId = wallet.Id, Type = TimeLedgerType.Consumption,
                        MinutesDelta = -cover, BookingId = bookingId, UserId = userId, CreatedAt = now
                    });
                }
                result.CoveredMinutes = cover;

                var uncovered = dur - cover;
                result.UncoveredMinutes = uncovered;

                if (uncovered > 0)
                {
                    result.Settlement = settlement.ToString();

                    if (settlement == SettlementMode.Debt)
                    {
                        // A1: süre-borcu yalnızca abonelik geçmişi olana (en az bir Credit) açıktır.
                        // Hiç abonelik almamış müşteri borçlanamaz → peşin tahsil zorunlu.
                        var hasAny = await _context.TimeLedger
                            .AnyAsync(e => e.WalletId == wallet.Id && e.Type == TimeLedgerType.Credit);
                        if (!hasAny)
                            throw new BusinessRuleException("Abonelik geçmişi olmayan müşteri süre borçlanamaz; peşin tahsil edilmelidir.");

                        // Borca yaz: aşım SÜRE olarak borçlanılır (para değil). Para, ödeme anında operatör tarafından girilir.
                        _context.TimeLedger.Add(new TimeLedgerEntry
                        {
                            WalletId = wallet.Id, Type = TimeLedgerType.DebtCharge, MinutesDelta = uncovered,
                            BookingId = bookingId, Reason = "Kapsanmayan süre borcu", UserId = userId, CreatedAt = now
                        });
                        result.DebtedMinutes = uncovered;
                    }
                    else
                    {
                        // A2: öneri fiyatı YALNIZCA tam oturum (cover==0) için — aşan kısım = bağımsız bir oturum,
                        // "ilk saat + sonraki yarım saat" formülü doğru çalışır. Kısmi kapsamada aşım, oturumun
                        // ortasından bir parça olduğundan formülle önerilemez → tutar operatörden ZORUNLU gelir.
                        decimal price;
                        if (chargeAmount.HasValue)
                            price = chargeAmount.Value;
                        else if (cover == 0)
                            price = await _pricing.PriceForMinutes(uncovered);
                        else
                            throw new BusinessRuleException("Kısmi kapsamada tahsilat tutarı elle girilmelidir.");
                        if (price < 0m) throw new BusinessRuleException("Tahsilat tutarı negatif olamaz.");
                        result.Charged = price;

                        // Peşin satış kalıbı: Charge + Payment net 0 (borç yok, para izi kalır).
                        _context.CashLedger.Add(new CashLedgerEntry
                        {
                            WalletId = wallet.Id, Type = CashLedgerType.Charge, AmountDelta = -price,
                            BookingId = bookingId, Reason = "Kapsanmayan oturum süresi", UserId = userId, CreatedAt = now
                        });
                        _context.CashLedger.Add(new CashLedgerEntry
                        {
                            WalletId = wallet.Id, Type = CashLedgerType.Payment, AmountDelta = price,
                            BookingId = bookingId, Reason = "Oturum kapanış tahsilatı", UserId = userId, CreatedAt = now
                        });
                    }
                }

                // Gelir raporlaması (dashboard) için ücreti booking.Price'a da yansıt.
                booking.Price = result.Charged;
                booking.Status = BookingStatus.Completed;

                try
                {
                    await _context.SaveChangesAsync();   // tüketim/ücret defter satırları + booking finalize
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    // Eşzamanlı ikinci kapanış unique index'e takıldı → idempotent davran.
                    throw new BusinessRuleException("Bu oturum zaten kapatılmış.");
                }

                await RecomputeBalances(wallet);   // bakiyeleri defterden türet (elle ± yok)
                result.WalletAfter = await GetWallet(parentId.Value);
                return result;
            });
        }

        // Borç ödeme: tüm süre-borcu tek seferde kapanır (kısmi yok). Operatör para karşılığını girer.
        // Süre-borcu (DebtSettle) sıfırlanır + para izi Charge+Payment net 0 olarak kaydedilir.
        public async Task<WalletDto> SettleTimeDebt(int customerId, decimal amount, int? userId)
        {
            if (amount < 0m)
                throw new BusinessRuleException("Tahsilat tutarı negatif olamaz.");

            return await InTransaction(async () =>
            {
                var wallet = await GetOrCreateWallet(customerId, forUpdate: true);
                if (wallet.TimeDebtMinutes <= 0)
                    throw new BusinessRuleException("Bu müşterinin süre borcu bulunmuyor.");

                _context.TimeLedger.Add(new TimeLedgerEntry
                {
                    WalletId = wallet.Id, Type = TimeLedgerType.DebtSettle, MinutesDelta = -wallet.TimeDebtMinutes,
                    Reason = "Süre borcu ödendi", UserId = userId, CreatedAt = DateTime.UtcNow
                });

                if (amount > 0m)
                {
                    _context.CashLedger.Add(new CashLedgerEntry
                    {
                        WalletId = wallet.Id, Type = CashLedgerType.Charge, AmountDelta = -amount,
                        Reason = "Süre borcu tahsilatı", UserId = userId, CreatedAt = DateTime.UtcNow
                    });
                    _context.CashLedger.Add(new CashLedgerEntry
                    {
                        WalletId = wallet.Id, Type = CashLedgerType.Payment, AmountDelta = amount,
                        Reason = "Süre borcu tahsilatı", UserId = userId, CreatedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
                await RecomputeBalances(wallet);
                return await GetWallet(customerId);
            });
        }

        public async Task<WalletDto> AdjustTime(int customerId, int minutesDelta, string reason, int? userId)
        {
            if (minutesDelta == 0)
                throw new BusinessRuleException("Düzeltme miktarı sıfır olamaz.");

            return await InTransaction(async () =>
            {
                var wallet = await GetOrCreateWallet(customerId, forUpdate: true);
                _context.TimeLedger.Add(new TimeLedgerEntry
                {
                    WalletId = wallet.Id, Type = TimeLedgerType.Correction,
                    MinutesDelta = minutesDelta, Reason = reason, UserId = userId, CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                await RecomputeBalances(wallet);
                return await GetWallet(customerId);
            });
        }

        public async Task<WalletDto> AdjustCash(int customerId, decimal amountDelta, string reason, int? userId)
        {
            if (amountDelta == 0m)
                throw new BusinessRuleException("Düzeltme tutarı sıfır olamaz.");

            return await InTransaction(async () =>
            {
                var wallet = await GetOrCreateWallet(customerId, forUpdate: true);
                _context.CashLedger.Add(new CashLedgerEntry
                {
                    WalletId = wallet.Id, Type = CashLedgerType.Adjustment, AmountDelta = amountDelta,
                    Reason = reason, UserId = userId, CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                await RecomputeBalances(wallet);
                return await GetWallet(customerId);
            });
        }
    }
}
