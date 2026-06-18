-- =============================================================================
-- Cüzdan + İki Defter — Aşama 0: tablo oluşturma + backfill + reconciliation
-- =============================================================================
-- Tasarım: server/docs/wallet-redesign.md
--
-- Bu script schema-first ortamda CANLI Postgres'e ELLE uygulanır (EF migration yok).
-- Idempotent: tablolar IF NOT EXISTS; backfill yalnızca `wallets` BOŞSA çalışır.
-- Davranış değişmez — eski purchases/purchasepayments/purchasebookings dokunulmaz.
--
-- Kullanım:
--   psql "$DATABASE_URL" -f scripts/wallet-migration.sql
-- =============================================================================

BEGIN;

-- -----------------------------------------------------------------------------
-- 1) Tablolar
-- -----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.wallets (
    id                   serial PRIMARY KEY,
    customer_id          int NOT NULL UNIQUE REFERENCES public.customers(id),
    time_balance_minutes int NOT NULL DEFAULT 0,            -- Σ time_ledger (canlı oturum HARİÇ)
    cash_balance         numeric(12,2) NOT NULL DEFAULT 0,  -- Σ cash_ledger; NEGATİF = borç
    valid_from           timestamptz,                       -- abonelik penceresi (tek pencere)
    valid_to             timestamptz,
    updated_at           timestamptz NOT NULL DEFAULT now()
);

-- ZAMAN hesabı: abonelik kredisi (+) ve oturum tüketimi (−)
CREATE TABLE IF NOT EXISTS public.time_ledger (
    id            serial PRIMARY KEY,
    wallet_id     int NOT NULL REFERENCES public.wallets(id),
    type          text NOT NULL,                  -- Credit | Consumption | Correction | Refund
    minutes_delta int NOT NULL,                   -- + kredi, − tüketim
    package_id    int REFERENCES public.packages(id),
    booking_id    int REFERENCES public.bookings(id),
    reason        text,
    user_id       int,
    created_at    timestamptz NOT NULL DEFAULT now()
);

-- Bir booking yalnızca BİR kez tüketilebilir (idempotent debit + backfill dedup)
CREATE UNIQUE INDEX IF NOT EXISTS ux_time_consumption_booking
    ON public.time_ledger (booking_id) WHERE type = 'Consumption';
CREATE INDEX IF NOT EXISTS ix_time_ledger_wallet ON public.time_ledger (wallet_id);

-- PARA/BORÇ hesabı: hizmet ücreti (−Charge) ve tahsilat (+Payment)
CREATE TABLE IF NOT EXISTS public.cash_ledger (
    id           serial PRIMARY KEY,
    wallet_id    int NOT NULL REFERENCES public.wallets(id),
    type         text NOT NULL,                   -- Charge | Payment | Adjustment | Refund
    amount_delta numeric(12,2) NOT NULL,          -- Charge − , Payment +
    booking_id   int REFERENCES public.bookings(id),
    reason       text,
    user_id      int,
    created_at   timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_cash_ledger_wallet ON public.cash_ledger (wallet_id);

-- -----------------------------------------------------------------------------
-- 2) Backfill  (yalnızca wallets boşsa — script tekrar çalıştırılabilir)
-- -----------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM public.wallets) THEN
        RAISE NOTICE 'wallets dolu — backfill atlandı.';
        RETURN;
    END IF;

    -- 2a) Paketi/ödemesi olan her müşteri için bir cüzdan.
    --     valid_from/valid_to = o müşterinin en GÜNCEL biten paketinden (tek pencere).
    INSERT INTO public.wallets (customer_id, valid_from, valid_to)
    SELECT p.customerid,
           win.startdate,
           win.enddate
    FROM (SELECT DISTINCT customerid FROM public.purchases) p
    LEFT JOIN LATERAL (
        SELECT pp.startdate, pp.enddate
        FROM public.purchases pp
        WHERE pp.customerid = p.customerid
        ORDER BY pp.enddate DESC NULLS LAST, pp.createdat DESC
        LIMIT 1
    ) win ON true;

    -- 2b) Her purchase → Credit (saat → dakika). package_id yok (eski sistemde FK yoktu).
    INSERT INTO public.time_ledger (wallet_id, type, minutes_delta, created_at)
    SELECT w.id, 'Credit', round(pu.hours * 60)::int, COALESCE(pu.createdat, now())
    FROM public.purchases pu
    JOIN public.wallets w ON w.customer_id = pu.customerid;

    -- 2c) Her purchasepayment → Credit (topup).
    INSERT INTO public.time_ledger (wallet_id, type, minutes_delta, created_at)
    SELECT w.id, 'Credit', round(ppy.hours * 60)::int, now()
    FROM public.purchasepayments ppy
    JOIN public.purchases pu ON pu.id = ppy.purchaseid
    JOIN public.wallets w     ON w.customer_id = pu.customerid;

    -- 2d) Her purchasebooking + booking sub-süresi → Consumption (− dakika).
    --     Yalnızca her iki sub-zaman da dolu olanlar (eski UsedMinutesFor mantığı).
    --     ux index sayesinde aynı booking iki kez yazılamaz.
    INSERT INTO public.time_ledger (wallet_id, type, minutes_delta, booking_id, created_at)
    SELECT DISTINCT ON (b.id)
           w.id,
           'Consumption',
           -round(EXTRACT(EPOCH FROM (b.subscriptionendtime - b.subscriptionstarttime)) / 60.0)::int,
           b.id,
           COALESCE(b.subscriptionendtime, now())
    FROM public.purchasebookings pb
    JOIN public.purchases pu ON pu.id = pb.purchaseid
    JOIN public.wallets   w  ON w.customer_id = pu.customerid
    JOIN public.bookings  b  ON b.id = pb.bookingid
    WHERE b.subscriptionstarttime IS NOT NULL
      AND b.subscriptionendtime   IS NOT NULL
    ORDER BY b.id;

    -- 2e) Materyalize bakiye = Σ minutes_delta. (cash_balance 0 — eski sistemde borç yok.)
    UPDATE public.wallets w
    SET time_balance_minutes = COALESCE(s.total, 0),
        updated_at = now()
    FROM (
        SELECT wallet_id, SUM(minutes_delta) AS total
        FROM public.time_ledger
        GROUP BY wallet_id
    ) s
    WHERE s.wallet_id = w.id;

    RAISE NOTICE 'Backfill tamamlandı: % cüzdan.', (SELECT count(*) FROM public.wallets);
END $$;

COMMIT;

-- =============================================================================
-- 3) RECONCILIATION  (ayrı çalıştır — 0 satır beklenir)
-- =============================================================================
-- Yeni time_balance_minutes ile eski formülü karşılaştırır:
--   eski_kalan = Σ(purchases.hours*60) + Σ(payments.hours*60) − Σ(used_minutes)
-- 1 dakikadan büyük fark (yuvarlama toleransı) anormaldir.
--
-- SELECT * FROM (
--     WITH credit AS (
--         SELECT pu.customerid,
--                SUM(round(pu.hours*60)) AS purchase_min,
--                COALESCE((SELECT SUM(round(ppy.hours*60))
--                          FROM public.purchasepayments ppy
--                          JOIN public.purchases p2 ON p2.id = ppy.purchaseid
--                          WHERE p2.customerid = pu.customerid), 0) AS payment_min
--         FROM public.purchases pu
--         GROUP BY pu.customerid
--     ),
--     used AS (
--         SELECT pu.customerid,
--                COALESCE(SUM(round(EXTRACT(EPOCH FROM (b.subscriptionendtime - b.subscriptionstarttime))/60.0)), 0) AS used_min
--         FROM public.purchasebookings pb
--         JOIN public.purchases pu ON pu.id = pb.purchaseid
--         JOIN public.bookings  b  ON b.id = pb.bookingid
--         WHERE b.subscriptionstarttime IS NOT NULL AND b.subscriptionendtime IS NOT NULL
--         GROUP BY pu.customerid
--     )
--     SELECT c.customerid,
--            (c.purchase_min + c.payment_min - COALESCE(u.used_min,0)) AS old_remaining,
--            w.time_balance_minutes AS new_balance,
--            (c.purchase_min + c.payment_min - COALESCE(u.used_min,0)) - w.time_balance_minutes AS diff
--     FROM credit c
--     JOIN public.wallets w ON w.customer_id = c.customerid
--     LEFT JOIN used u ON u.customerid = c.customerid
-- ) recon
-- WHERE abs(diff) > 1;
-- =============================================================================
