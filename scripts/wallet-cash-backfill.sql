-- =============================================================================
-- Cüzdan — Aşama 4b-para: cash_ledger tarihsel backfill
-- =============================================================================
-- Tasarım: server/docs/wallet-redesign.md · Durum: server/docs/wallet-status.md
--
-- Eski purchases/purchasepayments PARASINI cash_ledger'a yansıtır: her satış için
-- Charge(−price) + Payment(+price) → net 0 (peşin satış; borç doğurmaz).
-- Stage 0 backfill'i yalnız time_ledger'ı doldurmuştu (cash_balance 0); bu script para
-- tarafını tamamlar → dashboard PARA metrikleri ledger'dan okunabilir, eski tablolar 4d DROP'a hazır olur.
--
-- ÇİFT SAYIM KORUMASI / Idempotent: cash_ledger BOŞSA çalışır. Post-migration CreditTime
-- satışları (Charge/Payment) zaten cash_ledger'da olsaydı bu script DOKUNMAZDI — o durumda
-- elle gözden geçir (canlı dev DB'de şu an cash_ledger boş, doğrulandı).
--
-- KAPSAM (karar): hem purchases HEM purchasepayments backfill edilir — çünkü ileriye dönük
-- davranış (PurchasePaymentService → CreditTime) topup'ı da cash satışı olarak yazıyor ve
-- Stage 0 zaman backfill'i de payment'ı Credit olarak saymıştı. Sonuç: dashboard purchaseRevenue
-- = Σ(purchases.price)+Σ(payments.price). (Eski dashboard payment'ı görmezden geliyordu.)
--
-- Kullanım:  psql "$DATABASE_URL" -f scripts/wallet-cash-backfill.sql
--   conn: appsettings.Development.json (iroh_db@localhost)
-- =============================================================================

BEGIN;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM public.cash_ledger) THEN
        RAISE NOTICE 'cash_ledger dolu — backfill ATLANDI (çift sayım koruması).';
        RETURN;
    END IF;

    -- 1) Her purchase → kredi satışı: Charge(−price) + Payment(+price), net 0.
    --    Tarih = purchase.createdat. booking_id NULL → dashboard "kredi satışı" imzası.
    INSERT INTO public.cash_ledger (wallet_id, type, amount_delta, reason, created_at)
    SELECT w.id, 'Charge', -pu.price, 'Abonelik/kredi satışı (backfill)', COALESCE(pu.createdat, now())
    FROM public.purchases pu
    JOIN public.wallets w ON w.customer_id = pu.customerid;

    INSERT INTO public.cash_ledger (wallet_id, type, amount_delta, reason, created_at)
    SELECT w.id, 'Payment', pu.price, 'Abonelik/kredi tahsilatı (backfill)', COALESCE(pu.createdat, now())
    FROM public.purchases pu
    JOIN public.wallets w ON w.customer_id = pu.customerid;

    -- 2) Her purchasepayment (topup) → aynı model. Kendi tarihi YOK → parent purchase.createdat.
    INSERT INTO public.cash_ledger (wallet_id, type, amount_delta, reason, created_at)
    SELECT w.id, 'Charge', -ppy.price, 'Topup satışı (backfill)', COALESCE(pu.createdat, now())
    FROM public.purchasepayments ppy
    JOIN public.purchases pu ON pu.id = ppy.purchaseid
    JOIN public.wallets w ON w.customer_id = pu.customerid;

    INSERT INTO public.cash_ledger (wallet_id, type, amount_delta, reason, created_at)
    SELECT w.id, 'Payment', ppy.price, 'Topup tahsilatı (backfill)', COALESCE(pu.createdat, now())
    FROM public.purchasepayments ppy
    JOIN public.purchases pu ON pu.id = ppy.purchaseid
    JOIN public.wallets w ON w.customer_id = pu.customerid;

    -- 3) Materyalize cash_balance = Σ cash_ledger. Net 0 → 0 kalır (borç doğmaz); invariant korunur.
    UPDATE public.wallets w
    SET cash_balance = COALESCE(s.total, 0), updated_at = now()
    FROM (
        SELECT wallet_id, SUM(amount_delta) AS total
        FROM public.cash_ledger
        GROUP BY wallet_id
    ) s
    WHERE s.wallet_id = w.id;

    RAISE NOTICE 'cash_ledger backfill tamam: % satır, % cüzdan.',
        (SELECT count(*) FROM public.cash_ledger),
        (SELECT count(DISTINCT wallet_id) FROM public.cash_ledger);
END $$;

COMMIT;

-- =============================================================================
-- RECONCILIATION (ayrı çalıştır — beklenenler):
--   eski_toplam (purchases+payments) == yeni_ledger (kredi satışı Charge imzası)
--   toplam_bakiye == 0 (peşin model, borç yok)
-- =============================================================================
-- SELECT
--   (SELECT COALESCE(sum(price),0) FROM purchases)
--     + (SELECT COALESCE(sum(price),0) FROM purchasepayments)            AS eski_toplam,
--   (SELECT COALESCE(-sum(amount_delta),0) FROM cash_ledger
--      WHERE type='Charge' AND booking_id IS NULL)                       AS yeni_ledger,
--   (SELECT COALESCE(sum(cash_balance),0) FROM wallets)                  AS toplam_bakiye_0_olmali;
-- =============================================================================
