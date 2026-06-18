# Abonelik / Satın Alım Yeniden Tasarımı — Cüzdan + İki Defter

> Durum: **Tasarım onaylandı, uygulama başlıyor.**
> Kapsam: `Purchase` / `PurchasePayment` / `PurchaseBooking` ekosisteminin yerine müşteri başına
> **bir cüzdan + iki append-only defter** (zaman ve para/borç) modeli.

Bu doküman mevcut sistemin neden değiştiğini, hedef modeli, oturum-kapanış akışını, migration
stratejisini ve aşamalı geçiş planını anlatır. Kararlar bölümü, tasarımı şekillendiren iş kurallarını
ve seçimlerini kaydeder.

---

## 1. Neden değişiyor — mevcut sistemin sorunları

Mevcut tasarım: müşteri "saat" satın alır (`purchases`), bittiğinde "ek ödeme" (`purchasepayments`)
ile saat ekler, rezervasyonlar (`purchasebookings` → `bookings.subscriptionstarttime/endtime`) bu
saatleri tüketir. Bakiye hiçbir yerde saklanmaz; her seferinde join'lerle yeniden hesaplanır.

Tespit edilen başlıca sorunlar:

1. **Birim kaosu.** `purchases.hours` saat tutar ama servis/DTO'daki `usedHours` aslında **dakika**
   taşır (kod yorumu bunu itiraf eder). Her yerde `*60` / `/60` dönüşümleri uçuşur
   (`SubscriptionService`, client `use-columns`, `collect-payment`, `active-sessions/utils`). Tek bir
   dönüşüm unutulduğunda sessiz yanlış faturalama olur.
2. **Bakiyenin tek doğru kaynağı yok.** `(hours + Σpayments)*60 − usedMinutes` aritmetiği üç ayrı
   yerde (backend `ComputeForParents`, backend `GetByCustomerId`, client `use-columns`) kopyalanmış.
   Üç kopya = sapma riski; statü kademesi bile uçlar arasında ayrışmış.
3. **`PurchasePayment` aşırı yüklü ve doğrulamasız.** Hem "gerçek ek saat satışı" hem "yazım hatası
   düzeltme aracı" (silme/düzenleme yasak olduğu için). FK kontrolü, pozitiflik kontrolü, hatta
   **zaman damgası bile yok**.
4. **Para ile zaman iç içe.** Her satırda `hours` + `price` birlikte; "tahsil edilen mi, borç mu",
   ödeme yöntemi, saatlik ücret gibi kavramlar yok. **Borç (tab) kavramı tamamen eksik.**
5. **Denetlenebilirlik yanlış yoldan.** Silme yasak + saat alanı kilitli + "düzeltmeyi sahte payment
   ile yap". Doğru çözüm append-only defter: düzeltme = negatif kayıt.
6. **"En iyi paket" tahmini kırılgan.** Birden çok paket olunca billing tek paketi
   `OrderBy(valid).Then(rem>0).Then(endDate)` ile seçer; kullanım her pakete ayrı atfedilir;
   "cüzdan" ile "paket başına" düşünce çelişir.
7. **Sihirli statü string'leri.** `"ActiveSubscriber"` vb. her yerde yeniden türetilir
   (`active-bookings` 5 dal, `search-unified` aynı 5 dal — ama ayrı kopyalar).

### Kritik davranışsal gerçek (korunacak)

Paket **aile (parent) seviyesinde** ve **canlı** tüketilir:
- **Finalize tüketim**: booking `Completed` olurken `subscriptionstarttime/endtime` set edilir; bu
  süreler `usedHours`'a toplanır.
- **Canlı tüketim**: o anda açık olan tüm kardeş oturumlarının `now − startTime`'ı client'ta overlay
  olarak hesaplanır (`active-sessions/utils.ts → groupRemainingMinutes`).

Yeni modelde **defter yalnızca finalize hareketleri** tutar; canlı overlay client'ta kalır. Cüzdan
**müşteri (parent) başına tek** ve çocuklar arasında paylaşımlıdır.

---

## 2. Hedef model — bir cüzdan, iki hesap

Her şey **`int` dakika** ve **`numeric` ₺** cinsinden tutulur. Saate çevirme yalnızca görüntüde
(`formatDuration`) yapılır.

| Hesap | Birim | Ne tutar | Bakiye |
|---|---|---|---|
| **Zaman defteri** (subscription) | dakika | abonelik kredisi (+), oturum tüketimi (−), düzeltme | `time_balance_minutes` |
| **Para/Borç defteri** (cari/tab) | ₺ | hizmet ücreti (−Charge), tahsilat (+Payment), düzeltme | `cash_balance` — **negatifse = borç** |

- Para ve zaman tamamen ayrı.
- **borç = `max(0, −cash_balance)`**.
- Bir müşteri aynı anda hem `ActiveSubscriber` hem **borçlu** olabilir (borç statüden bağımsız rozet).

### Şema (DDL)

```sql
CREATE TABLE public.wallets (
    id                   serial PRIMARY KEY,
    customer_id          int NOT NULL UNIQUE REFERENCES customers(id),
    time_balance_minutes int NOT NULL DEFAULT 0,          -- Σ time_ledger (canlı oturum HARİÇ)
    cash_balance         numeric(12,2) NOT NULL DEFAULT 0,-- Σ cash_ledger; NEGATİF = borç
    valid_from           timestamptz,                     -- abonelik penceresi (tek pencere)
    valid_to             timestamptz,
    updated_at           timestamptz NOT NULL DEFAULT now()
);

-- ZAMAN hesabı: abonelik kredisi + tüketim
CREATE TABLE public.time_ledger (
    id            bigserial PRIMARY KEY,
    wallet_id     int NOT NULL REFERENCES wallets(id),
    type          text NOT NULL,                 -- Credit | Consumption | Correction | Refund
    minutes_delta int NOT NULL,                  -- + kredi, − tüketim
    package_id    int REFERENCES packages(id),   -- Credit ise hangi katalog
    booking_id    int REFERENCES bookings(id),   -- Consumption ise hangi oturum
    reason        text,
    user_id       int,
    created_at    timestamptz NOT NULL DEFAULT now()
);
-- Bir booking yalnızca bir kez tüketilebilir (idempotent debit)
CREATE UNIQUE INDEX ux_time_consumption_booking ON time_ledger(booking_id) WHERE type = 'Consumption';
CREATE INDEX ix_time_ledger_wallet ON time_ledger(wallet_id);

-- PARA/BORÇ hesabı: ücret + tahsilat
CREATE TABLE public.cash_ledger (
    id           bigserial PRIMARY KEY,
    wallet_id    int NOT NULL REFERENCES wallets(id),
    type         text NOT NULL,                  -- Charge | Payment | Adjustment | Refund
    amount_delta numeric(12,2) NOT NULL,         -- Charge − , Payment +
    booking_id   int REFERENCES bookings(id),    -- oturum ücreti ise
    reason       text,
    user_id      int,
    created_at   timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_cash_ledger_wallet ON cash_ledger(wallet_id);
```

- `time_balance_minutes` ve `cash_balance` materyalize **cache**; defterler **otorite**. Tutarsızlık
  olursa defterden reconcile edilir.
- `Package` (zaten var) **gerçek katalog** rolüne yükselir: kredi eklerken `package_id` + dakika + ₺
  yazılır. "Şablon ama FK yok" tutarsızlığı biter.
- Kalemli borç ekstresi = `cash_ledger` listesi. "Hangi ücret ödendi" görüntüsü, Payment'ları en eski
  Charge'lara **FIFO** eşleyerek okuma anında hesaplanır (ekstra alan gerekmez).

---

## 3. Statü — tek enum, tek fonksiyon

```csharp
enum SubscriptionStatus { Customer, Subscriber, UpcomingSubscriber, OverageSubscriber, ActiveSubscriber }

static SubscriptionStatus Derive(int timeBalance, bool validNow, bool hasUpcoming, bool hasAny) =>
    validNow && timeBalance > 0 ? ActiveSubscriber   // geçerli + dakika var
  : validNow                    ? OverageSubscriber   // geçerli ama 0 → yeni kullanım ücrete/borca gider
  : hasUpcoming                 ? UpcomingSubscriber
  : hasAny                      ? Subscriber
  :                               Customer;
```

Mevcut 5 dalın birebir aynısı; fark **tek yerde** olması. `active-bookings`, `search-unified`,
customer-list hepsi bu fonksiyonu çağırır.

---

## 4. Oturum kapanış akışı (çekirdek iş kuralı)

Yeni uç: **`POST /api/booking/{id}/close`**, gövde `{ settlement: "PayNow" | "Debt" }`.

```
dur   = subscriptionEndTime − subscriptionStartTime          (dakika)
cover = validNow ? min(dur, max(0, time_balance)) : 0        // KISMİ KAPSAMA: BÖL

1) cover > 0  → time_ledger: Consumption(−cover, booking_id)
2) uncovered = dur − cover
   uncovered > 0:
     P = pricing(uncovered)                                  // sunucu-otoriter fiyat (bkz. §7 bağımlılık)
     PayNow → cash_ledger: Charge(−P, booking) + Payment(+P, booking)   // net 0, borç yok
     Debt   → cash_ledger: Charge(−P, booking)                          // borç += P
3) booking.Status = Completed
   balances güncellenir (tek transaction, ux index ile çift-debit engellenir)
```

- `uncovered == 0` (tam kapsanan abonelik oturumu) ise `settlement` **sorulmaz**. Operatöre yalnızca
  kapsanmayan süre olduğunda "şimdi mi tahsil, borç mu" sorulur.
- Eski sistemdeki "overage → saat ekle" mantığı kalkar; **kapsanmayan süre bir para ücretidir** ve bu
  ücret ya peşin ya borçtur. Overage ve borç tek mekanizmada birleşir.
- **Paylaşımlı aile oturumları**: kapsama, kapanış anındaki finalize `time_balance`'a göre hesaplanır.
  Kardeş oturumlar sırayla kapanır; ilk kapanan dakikayı alır, sonrakiler bakiye 0 ise ücrete/borca düşer.
  Canlı overlay (görüntü) client'ta kalır.

### Borç ödeme

**`POST /api/wallet/{customerId}/settle { amount }`** → `cash_ledger: Payment(+amount)`; borç 0'a doğru
iner. Serbest tutar (kısmi ödeme) desteklenir; görüntüde en eski Charge'lardan FIFO kapanır.

---

## 5. Backend değişiklikleri

- **`WalletService`** (eski `SubscriptionService` çekirdeği + para tarafı):
  - `GetWallet(customerId)` → iki bakiye, statü, borç, son hareketler.
  - `CreditTime(customerId, minutes, money, packageId, type, userId)` → abonelik/topup.
  - `CloseBooking(bookingId, settlement, userId)` → §4 algoritması (tek transaction, idempotent).
  - `Settle(customerId, amount, userId)` → borç ödeme.
  - `AdjustTime / AdjustCash(reason, userId)` → düzeltme. **Eski kilit & "silme yasak" kalkar**;
    düzeltme denetlenebilir tek satır olur.
- `ComputeForParents` → cüzdandan tek sorgu; **"en iyi paket" tahmini silinir**.
  `GetActiveBookings` / `ChildService.SearchUnified` / customer-list statüyü ortak `Derive`'dan alır.
- `BookingService.Update`'teki `PurchaseBooking` ekleme mantığı → close akışına taşınır.
- Controller: `WalletController` (`GET /wallet/{id}`, `POST .../credit`, `POST .../settle`,
  `POST .../adjust`) + `POST /api/booking/{id}/close`. Eski purchase uçları geçiş bitene dek korunur.
- Dashboard metrikleri (`purchaseRevenue`, `purchaseCount`, `subscriptionSessions`) ledger sorgularına
  bağlanır (`type`, `amount_delta` filtreleri).

---

## 6. Client değişiklikleri

- `api/purchase/*` → `api/wallet/*`.
  `WalletType { timeBalanceMinutes, cashBalance, debt, status, validFrom, validTo, timeLedger[], cashLedger[] }`.
- **`*60` / `/60` her yerden kalkar** (`use-columns`, `collect-payment`, `active-sessions/utils`,
  `customer-child-select`). Backend dakika verir; yalnızca `formatDuration` saate çevirir.
- **Purchases sayfası → "Cüzdan"**: 2 sekme — *Saat hareketleri* ve *Borç/Tahsilat* (ekstre + "Borç Öde").
  "+1 tooltip" / "best package" karmaşası gider.
- **Oturum kapatma diyaloğu**: kapsanmayan süre varsa "Şimdi Tahsil / Borca Yaz" seçimi + tutar;
  tam kapsanıyorsa tek tık.
- Müşteri/oturum kartlarında **borç rozeti** (`debt > 0`). Canlı paylaşımlı oturum overlay'i
  (`groupRemainingMinutes`) **aynen korunur**, kaynağı `timeBalanceMinutes` olur.
- `edit` ekranındaki kilit mantığı (`isLocked`) silinir; düzeltme `adjust` ile yapılır.

---

## 7. Migration / backfill (schema-first, canlı DB, EF migration yok)

EF migration yok; canlı Postgres canonical. SQL elle uygulanır.
`scripts/wallet-migration.sql` (idempotent):

1. 3 tabloyu yarat (§2 DDL).
2. Her müşteriye bir `wallet`. Her `purchase` → `time_ledger.Credit`
   (`minutes = hours*60`, `package_id` varsa); her `purchasepayment` → `Credit` (topup);
   her `purchasebooking` + booking sub-süresi → `time_ledger.Consumption`.
3. `time_balance_minutes` = Σ minutes_delta; `valid_from/to` = en güncel paketten.
4. **`cash_ledger` boş başlar** — eski sistemde borç kavramı yok. `cash_balance = 0`.
5. **Reconciliation**: yeni `time_balance_minutes` ile eski formül
   (`(hours + Σpay)*60 − usedMinutes`) müşteri bazında karşılaştırılır (emekli `parity-check.sh`
   felsefesinin yerine geçer).

**Bağımlılık (flag):** Kapsanmayan sürenin ₺ fiyatı (`pricing(uncovered)`) şu an client'ta hesaplanıyor
(`firstHour` / `additionalHalfHour`). Borç tutarının güvenilir olması için bu pricing'in **sunucu
tarafında** (muhtemelen `Company` ayarlarında) olması/taşınması gerekir — Aşama 2'nin ön-işi.
Mevcut server-side pricing'in olup olmadığı kontrol edilip netleştirilecek.

---

## 8. Aşamalı geçiş planı

| Aşama | İçerik | Çıktı / doğrulama |
|---|---|---|
| **0** | Tablolar + backfill + reconciliation script | Davranış değişmez; reconciliation yeşil |
| **1** | Backend dual-write (kredi/tüketim hem eski tablolara hem ledger'a); okuma eski | `dotnet build` + xUnit + reconciliation yeşil |
| **2** | Okuma ledger'a döner; statü tek fonksiyon; `close` endpoint + server-side pricing | API çıktısı eski ile eşdeğer |
| **3** | Client cüzdan/ekstre UI + `*60` temizliği + borç akışı | `npm run build` + `npm run lint` |
| **4** | Eski purchase tablo/uçları emekli (arşiv ya da tam taşıma) | Ölü kod temizliği |

Her aşama bağımsız test edilebilir; istenen yerde durulabilir.

---

## 9. Kararlar kaydı

| # | Karar | Seçim | Gerekçe |
|---|---|---|---|
| 1 | Bakiye modeli | **Tek cüzdan (wallet)** | Müşterinin tek dakika bakiyesi; "en iyi paket" tahmini kalkar |
| 2 | Geçerlilik | **Tek pencere** (`valid_from/valid_to`) | En basit; preview'daki `expiresAt` ile uyumlu. (FIFO per-credit ileride eklenebilir) |
| 3 | Borç (tab) | **Birinci sınıf**: ayrı para/borç defteri | Beklenti 3-4-5; eski sistemde hiç yoktu |
| 4 | Kısmi kapsama | **Böl** | Kalan dakika abonelikten düşer, üstü ücret/borç; "kullanımlar abonelik saatlerinden" ile uyumlu, adil |
| 5 | Borç takibi | **Tek cari bakiye + kalemli ekstre**, serbest tutar, FIFO kapanış | Esnek, basit; kısmi ödeme serbest |
| 6 | Kapanışta tahsilat | **Yalnızca kapsanmayan süre varsa** PayNow/Debt sorulur | Beklenti 5 |
| 7 | Birim | **int dakika / numeric ₺** | `*60`//`60` kaosunu bitirir |

### İleride çıkabilecek senaryolar (model bunları ek bir satır tipiyle karşılar)

- Abonelik dışı normal (peşin) müşteri → sadece `cash_ledger` Charge + Payment.
- İade → `Refund` satırı (zaman veya para).
- Abonelik iptali / dondurma → `Adjustment` / `valid_to` güncelleme.
- Çoklu çocuk paylaşımı → zaten aile-seviyesi cüzdan; canlı overlay korunur.
