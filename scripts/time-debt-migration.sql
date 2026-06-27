-- =============================================================================
-- Süre-Borcu (Time-Debt) Modeli — şema değişikliği
-- =============================================================================
-- Tasarım: aşım (kapsanmayan süre) "Borca Yaz"da PARA değil SÜRE olarak borçlanılır;
-- ödeme anında operatör para karşılığını girer. Borç ayrı bir materyalize alanda tutulur.
--
-- Bu script schema-first ortamda CANLI Postgres'e ELLE uygulanır (EF migration yok).
-- Idempotent: IF NOT EXISTS ile güvenli, tekrar çalıştırılabilir.
--
-- Kullanım:
--   psql "$CONNSTR" -f server/scripts/time-debt-migration.sql
-- Dev: iroh_db @ localhost:5432
-- =============================================================================

BEGIN;

-- 1) Cüzdana süre-borcu materyalize alanı. Σ time_ledger {DebtCharge(+), DebtSettle(−)} (≥0).
--    Para-borcundan (cash_balance) bağımsızdır.
ALTER TABLE public.wallets
    ADD COLUMN IF NOT EXISTS time_debt_minutes int NOT NULL DEFAULT 0;

-- 2) Aşım borcunun aynı oturum için iki kez yazılmasını önle (idempotency sertleştirme).
--    Mevcut ux_time_consumption_booking (Consumption) kalıbının eşi.
CREATE UNIQUE INDEX IF NOT EXISTS ux_time_debtcharge_booking
    ON public.time_ledger (booking_id)
    WHERE type = 'DebtCharge';

COMMIT;

-- Not: time_ledger.type 'character varying' olduğundan yeni 'DebtCharge'/'DebtSettle'
-- string değerleri için ek şema değişikliği gerekmez (TimeLedgerType HasConversion<string>).
