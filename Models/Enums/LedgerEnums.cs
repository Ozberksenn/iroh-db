namespace Iroh.Models.Enums
{
    // Zaman defteri (subscription dakikaları) hareket tipleri.
    public enum TimeLedgerType
    {
        Credit,       // abonelik / topup → + dakika
        Consumption,  // oturum tüketimi → − dakika
        Correction,   // denetlenebilir düzeltme (eski "kilit" yerine)
        Refund        // iade → + dakika
    }

    // Para/borç defteri hareket tipleri. amount_delta: Charge −, Payment +.
    public enum CashLedgerType
    {
        Charge,       // kapsanmayan hizmet ücreti → −
        Payment,      // tahsilat → +
        Adjustment,   // denetlenebilir düzeltme
        Refund        // para iadesi → −
    }

    // Tek abone statüsü — eski 5 dallı mantığın tek karşılığı (active-bookings + search-unified ortak).
    public enum SubscriptionStatus
    {
        Customer,             // hiç paket yok
        Subscriber,           // paketi var ama hiçbiri geçerli değil (süresi dolmuş)
        UpcomingSubscriber,   // geçerli paket yok ama ileri tarihli var
        OverageSubscriber,    // geçerli + bakiye 0 → yeni kullanım ücrete/borca gider
        ActiveSubscriber      // geçerli + bakiye var
    }

    // Oturum kapanışında kapsanmayan süre için tahsilat kararı.
    public enum SettlementMode
    {
        PayNow,  // hemen tahsil → Charge + Payment (borç yok)
        Debt     // borca yaz → sadece Charge
    }
}
