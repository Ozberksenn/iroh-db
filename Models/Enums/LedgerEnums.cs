namespace Iroh.Models.Enums
{
    // Zaman defteri (subscription dakikaları) hareket tipleri.
    public enum TimeLedgerType
    {
        Credit,       // abonelik / topup → + dakika
        Consumption,  // oturum tüketimi → − dakika
        Correction,   // denetlenebilir düzeltme (eski "kilit" yerine)
        Refund,       // iade → + dakika
        // --- Süre-borcu defteri (time_debt_minutes'i besler; balance'a karışmaz) ---
        DebtCharge,   // kapsanmayan (aşım) süre borca yazıldı → + borç dakika
        DebtSettle    // borç netleme (kredi) / para ödemesi ile kapanış → − borç dakika
    }

    // Para/borç defteri hareket tipleri. amount_delta: Charge −, Payment +.
    public enum CashLedgerType
    {
        Charge,       // kapsanmayan hizmet ücreti → −
        Payment,      // tahsilat → +
        Adjustment,   // denetlenebilir düzeltme
        Refund        // para iadesi → −
    }

    // Abone statüsü — YALNIZ pencere + abonelik geçmişine göre türetilir. Bakiye statüyü belirlemez;
    // kalan dakika ayrı bir sayı, süre-borcu (TimeDebtMinutes) ayrı bir sinyaldir.
    public enum SubscriptionStatus
    {
        Customer,             // hiç abonelik almamış (Credit yok)
        Subscriber,           // abonelik geçmişi var ama şu an geçerli pencere yok (süresi dolmuş)
        UpcomingSubscriber,   // ileri tarihli (henüz başlamamış) pencere var
        ActiveSubscriber      // şu an geçerli abonelik penceresi var (bakiye 0 olsa da)
    }

    // Oturum kapanışında kapsanmayan süre için tahsilat kararı.
    public enum SettlementMode
    {
        PayNow,  // hemen tahsil → Charge + Payment (borç yok)
        Debt     // borca yaz → sadece Charge
    }
}
