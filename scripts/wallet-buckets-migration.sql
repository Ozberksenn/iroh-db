-- Aşama B: abonelik kovaları (docs/subscription-buckets.md)
-- time_ledger.Credit satırlarına per-kova geçerlilik penceresi ekler.
-- Schema-first: EF migration YOK; bu script canonical DB'ye uygulanır.
-- Kolon tipi wallets.valid_from ile AYNI: timestamptz (information_schema ile doğrulandı 2026-06-28).
-- App non-legacy Npgsql + DateTime.UtcNow (Kind=Utc) yazar → timestamptz zorunlu.

BEGIN;

ALTER TABLE public.time_ledger
    ADD COLUMN IF NOT EXISTS valid_from timestamptz NULL,
    ADD COLUMN IF NOT EXISTS valid_to   timestamptz NULL;

-- Backfill: mevcut her Credit satırına, ait olduğu cüzdanın eski TEK penceresini uygula.
-- (Aşama A öncesi tüm krediler tek pencereyi paylaşıyordu → geçmiş davranış korunur.)
UPDATE public.time_ledger AS tl
SET valid_from = w.valid_from,
    valid_to   = w.valid_to
FROM public.wallets AS w
WHERE tl.wallet_id = w.id
  AND tl.type = 'Credit'
  AND tl.valid_from IS NULL
  AND tl.valid_to IS NULL;

COMMIT;

-- Geri alma (rollback):
-- ALTER TABLE public.time_ledger DROP COLUMN IF EXISTS valid_from, DROP COLUMN IF EXISTS valid_to;
