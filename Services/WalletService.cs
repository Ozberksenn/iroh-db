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
        // Tamamlama tek uç: booking alanlarını (sub-time/endTime/not/masa/çocuk) da finalize eder.
        Task<CloseBookingResultDto> CloseBooking(int bookingId, SettlementMode settlement, int? userId,
            DateTime? subscriptionEndTime = null, DateTime? endTime = null, string? note = null, int? tableId = null, int? childId = null,
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

        // ---- statü türetimi: eski 5 dallı mantığın TEK karşılığı ----
        public static SubscriptionStatus Derive(int timeBalance, bool validNow, bool hasUpcoming, bool hasAny) =>
            validNow && timeBalance > 0 ? SubscriptionStatus.ActiveSubscriber
          : validNow                    ? SubscriptionStatus.OverageSubscriber
          : hasUpcoming                 ? SubscriptionStatus.UpcomingSubscriber
          : hasAny                      ? SubscriptionStatus.Subscriber
          :                               SubscriptionStatus.Customer;

        private static bool IsValidNow(Wallet w, DateTime now) =>
            w.ValidFrom.HasValue && w.ValidTo.HasValue && w.ValidFrom.Value <= now && w.ValidTo.Value >= now;

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
        private static readonly TimeLedgerType[] BalanceTypes =
            { TimeLedgerType.Credit, TimeLedgerType.Consumption, TimeLedgerType.Correction, TimeLedgerType.Refund };
        private static readonly TimeLedgerType[] DebtTypes =
            { TimeLedgerType.DebtCharge, TimeLedgerType.DebtSettle };

        private async Task RecomputeBalances(Wallet wallet)
        {
            // Kullanılabilir bakiye: yalnızca kredi/tüketim/düzeltme/iade.
            wallet.TimeBalanceMinutes = await _context.TimeLedger
                .Where(e => e.WalletId == wallet.Id && BalanceTypes.Contains(e.Type))
                .SumAsync(e => (int?)e.MinutesDelta) ?? 0;
            // Süre-borcu: DebtCharge(+) − DebtSettle(−) → kalan borçlu dakika (≥0).
            wallet.TimeDebtMinutes = await _context.TimeLedger
                .Where(e => e.WalletId == wallet.Id && DebtTypes.Contains(e.Type))
                .SumAsync(e => (int?)e.MinutesDelta) ?? 0;
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
            var validNow = IsValidNow(wallet, now);
            var hasUpcoming = wallet.ValidFrom.HasValue && wallet.ValidFrom.Value > now;
            var hasAny = timeRows.Any(e => e.Type == TimeLedgerType.Credit);
            var status = Derive(wallet.TimeBalanceMinutes, validNow, hasUpcoming, hasAny);

            return new WalletDto
            {
                Id = wallet.Id,
                CustomerId = wallet.CustomerId,
                TimeBalanceMinutes = wallet.TimeBalanceMinutes,
                TimeDebtMinutes = wallet.TimeDebtMinutes,
                CashBalance = wallet.CashBalance,
                ValidFrom = wallet.ValidFrom,
                ValidTo = wallet.ValidTo,
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
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // Geçerlilik penceresi verildiyse güncelle (tek pencere modeli; bakiyeden bağımsız alan).
                if (validFrom.HasValue) wallet.ValidFrom = validFrom;
                if (validTo.HasValue) wallet.ValidTo = validTo;

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
            DateTime? subscriptionEndTime = null, DateTime? endTime = null, string? note = null, int? tableId = null, int? childId = null,
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
                // Finalize zaman penceresi: client TAM süreyi gönderir; kapsama BÖL'ünü servis yapar.
                if (!booking.SubscriptionStartTime.HasValue)
                    booking.SubscriptionStartTime = booking.StartTime ?? now;
                booking.SubscriptionEndTime = subscriptionEndTime ?? booking.SubscriptionEndTime ?? now;
                booking.EndTime = endTime ?? booking.EndTime ?? now;

                var dur = (int)Math.Round((booking.SubscriptionEndTime!.Value - booking.SubscriptionStartTime!.Value).TotalMinutes);
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

                var validNow = IsValidNow(wallet, now);
                var cover = validNow ? Math.Min(dur, Math.Max(0, wallet.TimeBalanceMinutes)) : 0;

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
                        // Peşin tahsil: ücret operatör girdisi (chargeAmount); verilmezse Company oranı (öneri/varsayılan).
                        var price = chargeAmount ?? await _pricing.PriceForMinutes(uncovered);
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
