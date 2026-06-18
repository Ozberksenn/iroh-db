# Cüzdan Yeniden Tasarımı — DURUM & DEVAM NOTU

> Bu, "nerede kaldık / ne kaldı" özetidir. Tam tasarım gerekçeleri: **`wallet-redesign.md`**.
> Son güncelleme: 2026-06-18.

## Amaç (tek cümle)
Eski `Purchase`/`PurchasePayment`/`PurchaseBooking` sistemini **müşteri başına tek cüzdan + iki append-only defter** (zaman = dakika, para/borç = ₺) ile değiştirmek. Birim kaosu (`hours` vs `usedHours=dakika`), 3 yerde kopyalanan bakiye aritmetiği ve borç kavramının olmaması çözüldü.

## Kilitlenen kararlar
1. Tek cüzdan (en-iyi-paket tahmini yok) · 2. Tek geçerlilik penceresi (`valid_from/valid_to`) · 3. Borç birinci sınıf (ayrı `cash_ledger`, negatif bakiye = borç) · 4. Kısmi kapsamada **BÖL** (kalan dakika abonelikten, üstü ücret/borç) · 5. Borç = tek cari bakiye + kalemli ekstre, serbest tutar FIFO ödeme · 6. Kapanışta yalnızca kapsanmayan süre varsa "peşin mi borç mu" sorulur · 7. Birim: `int` dakika / `numeric` ₺. Fiyat `Company.FirstHourPrice/AdditionalHalfHourPrice`'tan.

---

## YAPILDI ✅ (backend + client uçtan uca, canlı dev DB'de doğrulandı)

| Aşama | İçerik | Anahtar dosyalar |
|---|---|---|
| **0 — DB** | 3 tablo + backfill + reconciliation (0 fark) **canlıya uygulandı** | `scripts/wallet-migration.sql` |
| **1 — Domain** | `Wallet`/`TimeLedgerEntry`/`CashLedgerEntry`, enum'lar, `WalletService` (GetWallet/CreditTime/CloseBooking/RecordConsumption/Settle/AdjustTime/AdjustCash), `PricingService` (Company'den), tek `Derive` statü | `Models/Entities/Wallet.cs`, `…/TimeLedgerEntry.cs`, `…/CashLedgerEntry.cs`, `Models/Enums/LedgerEnums.cs`, `Services/WalletService.cs`, `Services/PricingService.cs`, `Models/DTOs/Wallet/*` |
| **2 — API + okuma** | `WalletController` (GET/credit/settle/adjust), `POST /booking/{id}/close`, kredi+tüketim dual-write, `ComputeForParents` cüzdandan okur (sentetik BestPurchase), statü tek `Derive`'da (`active-bookings`/`search-unified`/`customer-list`) | `Controllers/WalletController.cs`, `Controllers/BookingController.cs`, `Services/SubscriptionService.cs`, `Services/ChildService.cs`, `Services/CustomerService.cs`, `Services/PurchaseService.cs`, `Services/PurchasePaymentService.cs`, `Services/BookingService.cs` |
| **3 — Client** | `api/wallet/*`, sekmeli **Cüzdan sayfası** (saat + borç/tahsilat ledger), **Kredi Ekle**/**Borç Öde**, oturum kapatmada **Borca Yaz / Şimdi Tahsil Et**, `/customers/:id/wallet` rotası + nav, en/tr i18n | `client/src/api/wallet/*`, `client/src/pages/wallet/*`, `client/src/pages/active-sessions/complete.tsx`, `client/src/core/user.config.ts`, `client/src/pages/customers/hooks/use-columns.tsx`, `client/src/locales/{en,tr}.json` |
| **4a — Client emekliye (TAM)** | Purchases sayfası + tüm kullanılmayan `api/purchase` hook'ları + route kaldırıldı; tooltip UX hatası düzeltildi (`purchasesTooltip`→`walletTooltip`, "Cüzdan"/"Wallet"); öksüz `purchases.*` i18n (70'er satır) temizlendi; active-sessions'ın 6 dosyasındaki **no-op** `useInvalidateCustomerPurchaseAll` çağrıları kaldırıldı → `customer-purchase-all.ts` silindi. **Client artık eski purchase API'sine HİÇ gitmiyor** (`GET /purchase/customer/{id}` dahil; yalnız `api/purchase/types.ts` session tipi için kalır). Doğrulama: build (tsc+vite) yeşil; lint 57 sorunun tamamı önceden-var-olan/ilgisiz | silindi: `pages/purchases/*`, `api/purchase/*` (`types.ts` hariç); düzenlendi: `core/user.config.ts`, `customers/hooks/use-columns.tsx`, `locales/{en,tr}.json`, `active-sessions/{cancel,complete,continue,create,pause,update}.tsx` |
| **4b-zaman — Dashboard zaman metrikleri** | `subscriptionSessions` + `IsSubscription` artık `PurchaseBookings` yerine `time_ledger` (`Type==Consumption`, distinct `BookingId`) okuyor (eski okuma migration sonrası latent kırıktı). Doğrulama: build 0 hata · 28 xUnit yeşil · **canlı DB parite 0 fark** (eski 4 = yeni 4) | `Services/DashboardService.cs` |
| **4c — Server legacy purchase yüzeyi kaldırıldı** | `PurchaseController`/`PurchasePaymentController` + `PurchaseService`/`PurchasePaymentService` (+DI) silindi; `BookingService.Update`'ten dual-write çağrısı + **ölü** `PurchaseBooking`-bağlama dalı + `_wallet` injection çıkarıldı (client bu uçları/alanı kullanmıyordu). Korundu: Purchase/PurchaseBooking/PurchasePayment **entity+DbSet** (dashboard para metrikleri), `WalletService.RecordConsumption` metodu+testi. Doğrulama: build 0 hata · 28 xUnit yeşil · 0 dangling referans | silindi: `Controllers/Purchase*Controller.cs`, `Services/Purchase*Service.cs`; düzenlendi: `Program.cs`, `Services/BookingService.cs` |
| **4b-para — Dashboard para metrikleri → ledger** | `wallet-cash-backfill.sql` (idempotent): tarihsel purchases+payments → `cash_ledger` Charge+Payment (net 0, borç yok); reconciliation 3400=3400, bakiye 0, canlıya uygulandı. DashboardService `purchaseRevenue`/`PurchaseCount`/TopCustomer/RevenueChart-purchase → `cash_ledger` (`Charge && booking_id null`) okuyor; artık `_context.Purchases` HİÇ okumuyor. Doğrulama: build 0 hata · 28 test · **canlı dashboard PASS** (purchaseRevenue 3400, count 4, topCustomers+revenueChart tutarlı, EF çevirisi OK) | `scripts/wallet-cash-backfill.sql`, `Services/DashboardService.cs` |

**Doğrulama:** 28 xUnit yeşil · client `npm run build` (tsc) + `npm run lint` temiz · canlı smoke'lar: `scripts/wallet-smoke.sh`, `scripts/wallet-close-smoke.sh` (ikisi de DB'ye yazıp geri alır).

---

## KALAN ⏳

- **Stage 4d — eski tabloları emekliye (KALAN tek ana iş):** DashboardService artık eski tabloları okumuyor. Geriye TEK bağ: `WalletService.RecordConsumption` hâlâ `PurchaseBookings`+`Purchases` okuyor (production'da ÖLÜ — 4c çağrıyı kaldırdı, yalnız xUnit testinde çağrılıyor). Sıra: (a) `RecordConsumption` metodu (`IWalletService`+impl) + `WalletServiceTests` testini kaldır → (b) Purchase/PurchaseBooking/PurchasePayment entity+DbSet'lerini kaldır → (c) `purchases`/`purchasepayments`/`purchasebookings` arşivle ya da DROP (canlı-DB). Opsiyonel ek: öksüz Purchase DTO'ları, vestigial `BookingUpdateDto.PurchaseId`.
- **Opsiyonel polish:** `active-sessions/utils.ts` + `components/customer-child-select.tsx` içindeki `*60`//`60` sadeleştirmesi. Sentetik `purchase` sözleşmesiyle çalışıyor; sadece temizlik.
- **Görsel doğrulama:** Cüzdan sayfası tarayıcıda açılıp gözle kontrol edilmedi (login gerekiyordu). `cd server && dotnet run` + `cd client && npm run dev` → müşteri listesinde 🧾 butonu.

## Bilinen mevcut sorun (tasarım dışı, dikkat)
Bazı `children.birth_date` DB'de NULL ama `Child.BirthDate` non-nullable `DateTime` → `Child` entity'sini tam yüklemek `InvalidCastException` atıyor. `CloseBooking` bunu `ParentId` projeksiyonuyla aşıyor. Kalıcı çözüm: entity'de `DateTime?` yapmak veya NULL'ları doldurmak. (`GetActiveBookings` `Include(Child)` kullanıyor — orada da patlayabilir, kontrol et.)

## Çalıştırma / doğrulama komutları
```bash
# backend
cd server && dotnet build iroh-be.sln && dotnet test tests/Iroh.Tests.csproj
./scripts/wallet-smoke.sh          # cüzdan API canlı smoke (geri alır)
./scripts/wallet-close-smoke.sh    # kapanış→borç canlı smoke (geri alır)
# client
cd client && npm run build && npm run lint
# DB migration (yeni ortamda)
psql "<conn>" -f server/scripts/wallet-migration.sql   # conn: appsettings.Development.json
```

## Sıradaki ilk adım (devam edince)
4a + 4b-zaman + 4c + 4b-para bitti — **dashboard dahil tüm sistem cüzdan/ledger'dan okuyor**, eski tablolara yazma YOK, `cash_ledger` backfill'i canlıya uygulandı (reconciliation 3400=3400). **KALAN tek ana iş: 4d** — eski tabloları emekliye ayır: önce `WalletService.RecordConsumption` (+xUnit testi) ve Purchase/PurchaseBooking/PurchasePayment entity+DbSet'lerini kaldır (RecordConsumption hâlâ PurchaseBookings okuyor ama production'da ölü), sonra üç tabloyu arşivle/DROP (canlı-DB; ayrı onay). Opsiyonel: öksüz Purchase DTO'ları (`CustomerPurchaseResultDto`/`PurchaseBookingResultDto`/`PurchaseListDto`), vestigial `BookingUpdateDto.PurchaseId`.
