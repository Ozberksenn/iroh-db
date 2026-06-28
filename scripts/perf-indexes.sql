-- perf-indexes.sql
-- Performans indexleri — sıcak filtre/join kolonları. Idempotent (IF NOT EXISTS), runtime maliyeti yok.
-- Hem dev DB'ye uygulanır hem prod bootstrap'ının (PLAN-PROD-LAUNCH.md Faz 2) parçasıdır.
--
-- NOT (kolon adlandırma): legacy (Node döneminden) kolonlar underscore'SUZ küçük harf
--   (bookings.starttime, bookings.tableid); cüzdan/Faz1 döneminde eklenenler underscore'LU
--   (bookings.child_id, cash_ledger.created_at). Aşağıdaki adlar canlı şemayla doğrulanmıştır.
--
-- NOT (canlı prod'a sonradan eklerken): trafik altındaki bir tabloyu kilitlememek için
--   "CREATE INDEX CONCURRENTLY" kullan (transaction bloğu dışında). Bootstrap deploy öncesi
--   çalıştığı için burada düz CREATE INDEX yeterli.
--
-- Zaten mevcut olanlar (tekrar oluşturulmaz): bookings(status), wallets(customer_id) uniq,
--   cash_ledger(wallet_id), time_ledger(wallet_id) + partial unique'ler.

-- children.parent_id — EN KRİTİK: "müşteriye ait booking'ler" ve dashboard'un müşteri-bazlı
-- tüm sorguları bu join'den geçer; şu an index yok.
CREATE INDEX IF NOT EXISTS ix_children_parent    ON children(parent_id);

-- bookings.starttime — dashboard tarih-aralığı taramaları + booking liste tarih filtresi.
CREATE INDEX IF NOT EXISTS ix_bookings_starttime ON bookings(starttime);

-- bookings.child_id — child bazlı join/filtre (customerId filtresi child üzerinden gider).
CREATE INDEX IF NOT EXISTS ix_bookings_child     ON bookings(child_id);

-- bookings.tableid — masa filtresi + masa-doluluk kontrolleri.
CREATE INDEX IF NOT EXISTS ix_bookings_table     ON bookings(tableid);

-- cash_ledger — dashboard kredi-satışı sorguları: type + booking_id eşitliği, created_at aralığı
-- (eşitlik kolonları önce, aralık kolonu sonda).
CREATE INDEX IF NOT EXISTS ix_cashledger_filter  ON cash_ledger(type, booking_id, created_at);
