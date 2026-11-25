-- Worker, bildirim göndermeden önce alıcının hâlâ grup üyesi ve e-postasının doğrulanmış olduğunu kontrol eder.
-- Kolon bazlı yetki: sadece kimlik, normalize e-posta ve doğrulama bayrağı okunabilir.
-- Şifre hash'i, güvenlik damgası, telefon vb. Worker'a kapalı kalır (least privilege).
GRANT SELECT ON auth.AspNetUsers (Id, NormalizedEmail, EmailConfirmed) TO zr_worker_role;
