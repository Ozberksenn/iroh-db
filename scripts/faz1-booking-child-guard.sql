-- Faz 1 / F1.1 — "Bir çocuk = en fazla bir açık (Active/Paused) seans" DB garantisi
-- + F1.3 — company fiyat kolonlarının numeric olduğunun garantisi
-- Schema-first: bu betik DB'ye ELLE uygulanır (EF migration yok). Idempotent yazıldı.
-- Uygulamadan önce dev/prod DB yedeği al.

-- ─────────────────────────────────────────────────────────────────────────────
-- 1) ÖN-KONTROL: index'i oluşturmadan önce mevcut ihlalleri bul.
--    Aşağıdaki sorgu SATIR DÖNERSE, o çocukların birden çok açık seansı var;
--    index oluşturma BU YÜZDEN HATA VERİR. Önce bu seansları kapat/iptal et.
--    (Sistem Misafiri 999999 ve child_id NULL hariç tutulur.)
-- ─────────────────────────────────────────────────────────────────────────────
-- SELECT child_id, COUNT(*) AS open_sessions
-- FROM public.bookings
-- WHERE status IN ('Active','Paused') AND child_id IS NOT NULL AND child_id <> 999999
-- GROUP BY child_id
-- HAVING COUNT(*) > 1;

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) Kısmi unique index: çocuk başına en fazla bir Active/Paused seans.
--    Misafir (999999) ve çocuksuz (NULL) girişler muaf.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE UNIQUE INDEX IF NOT EXISTS ux_one_open_booking_per_child
    ON public.bookings (child_id)
    WHERE status IN ('Active','Paused')
      AND child_id IS NOT NULL
      AND child_id <> 999999;   -- SystemConstants.GuestCustomerId

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) F1.3: company fiyat kolonları numeric değilse numeric(12,2)'ye çevir.
--    (Büyük olasılıkla zaten numeric; bu blok yalnızca integer ise dokunur.)
-- ─────────────────────────────────────────────────────────────────────────────
DO $$
BEGIN
    IF (SELECT data_type FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'company'
          AND column_name = 'firsthourprice') = 'integer' THEN
        ALTER TABLE public.company ALTER COLUMN firsthourprice TYPE numeric(12,2);
    END IF;

    IF (SELECT data_type FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'company'
          AND column_name = 'additionalhalfhourprice') = 'integer' THEN
        ALTER TABLE public.company ALTER COLUMN additionalhalfhourprice TYPE numeric(12,2);
    END IF;
END $$;
