-- ============================================================================
-- Kullanıcı rolü (RBAC) — şema-first kolon ekleme
-- ----------------------------------------------------------------------------
-- Bu proje schema-first çalışır (canlı DB kanonik, EF migration'ları şemayı
-- sürmez). Rol özelliği için `users` tablosuna `role` kolonu bu betikle,
-- ELLE uygulanır. Program.cs artık açılışta Database.Migrate() ÇAĞIRMAZ.
--
-- Enum DB'de string olarak tutulur: 'User' | 'Admin'
-- (AppDbContext: modelBuilder.Entity<User>().Property(u => u.Role).HasConversion<string>()).
--
-- Uygulama (idempotent):  psql "$CONNECTION_STRING" -f scripts/add_user_role_column.sql
-- ============================================================================

BEGIN;

-- 1) Kolonu ekle. Var olan satırlar güvenli varsayılan 'User' alır (boş string DEĞİL).
ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS role varchar(20) NOT NULL DEFAULT 'User';

-- 2) İlk yönetici(ler)i yükselt. E-postayı kendi admin hesabınızla değiştirin.
--    (Kolon ilk kez eklendiğinde herkes 'User' olur; en az bir Admin şart, yoksa
--     Ayarlar'a kimse erişemez.)
-- UPDATE public.users SET role = 'Admin' WHERE mail = 'admin@ornek.com';

-- 3) (İsteğe bağlı) Yalnızca beklenen değerlere izin veren bir CHECK kısıtı.
-- ALTER TABLE public.users
--     ADD CONSTRAINT chk_users_role CHECK (role IN ('User', 'Admin'));

COMMIT;

-- Doğrulama:
--   SELECT mail, role FROM public.users ORDER BY role DESC, mail;
