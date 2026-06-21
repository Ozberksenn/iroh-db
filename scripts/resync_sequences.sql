-- resync_sequences.sql
--
-- Amaç: public şemasındaki tüm 'id' kolonlarına bağlı serial/identity sequence'lerini
-- tablonun MAX(id) değerine senkronlar.
--
-- Neden gerekli: Seed/restore işlemleri kayıtları EXPLICIT id ile ekleyince sequence
-- ilerlemez (is_called=false / last_value < max(id)). Sonraki ilk INSERT, var olan bir
-- id'yi üretmeye çalışır → duplicate key violation → generic 500 (bkz. AUDIT-FINDINGS BUG-B).
-- Örn. company_id_seq: last_value=1, is_called=false, max(id)=1 → ilk company insert'i patlar.
--
-- Bu script idempotent ve güvenlidir: yalnızca sequence sayaçlarını doğru duruma getirir,
-- veriye dokunmaz. Her DB restore/reseed sonrası çalıştırılması önerilir.

DO $$
DECLARE
    r      RECORD;
    seq    TEXT;
    maxid  BIGINT;
BEGIN
    FOR r IN
        SELECT t.table_name
        FROM information_schema.tables t
        JOIN information_schema.columns c
          ON c.table_schema = t.table_schema
         AND c.table_name   = t.table_name
        WHERE t.table_schema = 'public'
          AND t.table_type   = 'BASE TABLE'
          AND c.column_name  = 'id'
    LOOP
        -- Yalnızca id kolonu bir serial/identity sequence'e bağlıysa (aksi NULL döner) işle.
        seq := pg_get_serial_sequence(format('public.%I', r.table_name), 'id');
        IF seq IS NOT NULL THEN
            -- SystemConstants.GuestCustomerId = 999999 REZERVE sentinel'dir (customers/children'a elle eklenir).
            -- Sequence doğal aralığı (1,2,3...) takip etmeli; sentinel'e atlarsa yeni kayıtlar 1000000'den
            -- başlar. Bu yüzden MAX hesabında 999999 hariç tutulur (başka tabloda 999999 id yoktur → etkisiz).
            EXECUTE format('SELECT COALESCE(MAX(id), 0) FROM public.%I WHERE id <> 999999', r.table_name) INTO maxid;
            IF maxid > 0 THEN
                -- is_called=true → bir sonraki nextval() = maxid + 1.
                PERFORM setval(seq, maxid, true);
            ELSE
                -- Boş tablo → is_called=false ile sıfırla; ilk nextval() = 1.
                PERFORM setval(seq, 1, false);
            END IF;
            RAISE NOTICE 'resync %.id -> max=% (seq=%)', r.table_name, maxid, seq;
        END IF;
    END LOOP;
END $$;
