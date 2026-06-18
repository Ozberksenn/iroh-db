-- =============================================================================
-- Cüzdan — Aşama 4d: eski Purchase tablolarını ARŞİVLE (rename)
-- =============================================================================
-- Tasarım: server/docs/wallet-redesign.md · Durum: server/docs/wallet-status.md
--
-- purchases / purchasepayments / purchasebookings tablolarını `_archived_*` olarak
-- yeniden adlandırır. Veri + FK + index KORUNUR (DROP değil — geri alınabilir).
-- .NET tarafı bu tablolara artık DbSet ile bağlı değil (4d kod adımları); EF onları yok sayar.
-- Tüm para/zaman verisi cash_ledger + time_ledger'a taşınmıştı (4b backfill, reconciliation 0 fark).
--
-- Idempotent: yalnızca orijinal ad hâlâ varsa rename eder (tekrar çalıştırılabilir).
-- Geri alma:  ALTER TABLE _archived_purchases RENAME TO purchases;  (vb.)
--
-- Kullanım:  psql "$DATABASE_URL" -f scripts/wallet-archive-legacy.sql
-- =============================================================================

BEGIN;

DO $$
BEGIN
    -- Çocuk tablolar önce (FK sırası rename'de zorunlu değil ama tutarlı dursun).
    IF to_regclass('public.purchasebookings') IS NOT NULL THEN
        ALTER TABLE public.purchasebookings RENAME TO _archived_purchasebookings;
        RAISE NOTICE 'purchasebookings → _archived_purchasebookings';
    END IF;

    IF to_regclass('public.purchasepayments') IS NOT NULL THEN
        ALTER TABLE public.purchasepayments RENAME TO _archived_purchasepayments;
        RAISE NOTICE 'purchasepayments → _archived_purchasepayments';
    END IF;

    IF to_regclass('public.purchases') IS NOT NULL THEN
        ALTER TABLE public.purchases RENAME TO _archived_purchases;
        RAISE NOTICE 'purchases → _archived_purchases';
    END IF;
END $$;

COMMIT;
