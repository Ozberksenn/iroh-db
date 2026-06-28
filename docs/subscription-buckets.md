# Abonelik kovaları (Aşama B)

## Problem
Aşama A'ya kadar cüzdan **tek** geçerlilik penceresi (`wallets.valid_from/valid_to`) taşıyordu ve her
`CreditTime` bu pencerenin üstüne yazıyordu (son-yazan-kazanır). Top-up sık olduğundan (kullanıcı kararı,
2026-06-28) bu yetersiz: aynı anda farklı pencereli birden çok paket olabilmeli ve süresi dolan paketin
kullanılmayan dakikaları **yanmalı**.

## Model — kova + yeniden-oynatma (etiketsiz)
- `time_ledger`'a **2 kolon** eklenir: `valid_from`, `valid_to`. Yalnız `Credit` satırlarında doludur ve o
  kredinin (kovanın) penceresini tanımlar. (`Consumption` vb. satırlarda NULL.)
- Bir **kova** = pencereli bir `Credit` satırı (M dakika, `[valid_from, valid_to]`; uçlar NULL = sınırsız).
- Tüketim satırı **tek** kalır (`Consumption = −cover`); kovaya **etiketlenmez**. Hangi kovadan düştüğü,
  `WalletService.ComputeBuckets` içindeki **kronolojik yeniden-oynatma** ile deterministik çözülür:
  - Satırlar `CreatedAt, Id`'ye göre sırayla işlenir.
  - `Credit` → yeni kova.
  - `Consumption (−m)` → o **anda geçerli** (`vf<=t<=vt`) kovalardan **önce-süresi-dolacak** (`valid_to` artan;
    NULL = sonsuz, en sona) sırayla `m` düşülür; kovalar yetmezse **havuz**dan.
  - `Correction/Refund (±m)` → süresiz **havuz**a yazılır (paket değil, manuel ayar → yanmaz).
  - `DebtCharge/DebtSettle` → süre-borcu defteri; bakiyeye karışmaz.
- `CloseBooking` ile yeniden-oynatma **aynı kuralı** (geçerli kovalardan önce-süresi-dolacak) kullandığından
  oynatma orijinal dağıtımı birebir yeniden üretir → tutarlı.

## Türetilen büyüklükler (`ComputeBuckets(rows, now)`)
- **AvailableNow** = `now`'da geçerli kovaların kalanı + havuz. → `wallet.TimeBalanceMinutes` (kapsama tabanı).
- **HasValidNow** = `now`'da geçerli (kalanına bakılmaksızın) bir `Credit` kovası var mı → statü `ActiveSubscriber`.
- **HasUpcoming** = `valid_from > now` olan kova var mı → `UpcomingSubscriber`.
- **HasAny** = hiç `Credit` var mı → `Subscriber` (süresi dolmuş) vs `Customer`.
- **NextExpiry** = kalanı olan geçerli kovaların en erken `valid_to`'su → "şu tarihte yanacak" uyarısı (A6).
- **Burned** = `valid_to < now` olan kovaların kalanı (bilgi amaçlı; kayıp).

Statü, A4'teki gibi yalnız pencere+geçmişten türetilir (`Derive(hasValidNow, hasUpcoming, hasAny)`); bakiye
statüyü belirlemez.

## `wallets` alanları (repurpose; yeni kolon YOK)
- `valid_from/valid_to` artık **görüntü amaçlı toplulaştırma**: süresi dolmamış kovaların `min(vf)` / `max(vt)`'si.
  `CreditTime` bunları artık doğrudan yazmaz; `RecomputeBalances` kovalardan türetir.
- Otoriter `validNow`/kapsama **kova** üzerinden hesaplanır (cüzdan toplu penceresi değil).

## Kapsama (`CloseBooking`)
`cover = min(dur, max(0, AvailableNow@now))`. Tek `Consumption(−cover)` yazılır. Kapsanmayan (`dur−cover`)
A1/A2 kuralıyla borç/peşin olur. Süresi dolmuş (geçerli kova yok) → AvailableNow 0 → cover 0 (eskisi gibi).

## DB göçü (`scripts/wallet-buckets-migration.sql`) — canonical DB'de elle çalıştırılır
1. `time_ledger`'a `valid_from`, `valid_to` kolonları (NULL'lanabilir). **Tip `wallets.valid_from` ile aynı olmalı.**
2. Backfill: her mevcut `Credit` satırına, ait olduğu cüzdanın eski tek penceresi (`wallets.valid_from/to`) yazılır
   → geçmiş veri eski davranışla birebir oynatılır.
EF migration yok (schema-first); bu script canonical DB'ye uygulanmadan kova kolonları olmadığı için API patlar.

## Test
`ComputeBuckets` saf fonksiyon → InMemory birim/entegrasyon testleri: çoklu kova kapsama, önce-süresi-dolacak,
yanma (expired kova kalanı AvailableNow'a girmez), havuz (Correction), upcoming. Uçtan-uca kapsama canonical
DB gerektirir.
