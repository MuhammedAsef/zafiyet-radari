-- Least privilege: uygulama rolleri ve şema bazlı yetkiler.
-- Login'ler ve kullanıcılar Migrator tarafından oluşturulup bu rollere eklenir.
-- Hiçbir uygulama rolünde DDL (CREATE / ALTER / DROP) yetkisi yoktur.
-- Şema bazlı GRANT, şemaya sonradan eklenen tablolara da otomatik uygulanır.

IF SCHEMA_ID(N'hangfire') IS NULL EXEC (N'CREATE SCHEMA hangfire');

IF DATABASE_PRINCIPAL_ID(N'zr_web_role') IS NULL CREATE ROLE zr_web_role;
IF DATABASE_PRINCIPAL_ID(N'zr_worker_role') IS NULL CREATE ROLE zr_worker_role;

-- Web: kullanıcı işlemleri
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::inventory TO zr_web_role;
GRANT SELECT, INSERT, UPDATE         ON SCHEMA::finding   TO zr_web_role;
GRANT SELECT                         ON SCHEMA::vuln      TO zr_web_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::notify    TO zr_web_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::auth      TO zr_web_role;
GRANT SELECT, INSERT                 ON SCHEMA::ops       TO zr_web_role;

-- Worker: veri senkronizasyonu, eşleştirme, bildirim
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::vuln      TO zr_worker_role;
GRANT SELECT, INSERT, UPDATE         ON SCHEMA::finding   TO zr_worker_role;
GRANT SELECT                         ON SCHEMA::inventory TO zr_worker_role;
GRANT SELECT, INSERT, UPDATE         ON SCHEMA::notify    TO zr_worker_role;
GRANT SELECT, INSERT, UPDATE         ON SCHEMA::ops       TO zr_worker_role;

-- Hangfire: Web iş kuyruğa atar, Worker çalıştırır. Tabloları Migrator kurar.
GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::hangfire TO zr_web_role;
GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::hangfire TO zr_worker_role;

-- Not: ops.AuditLog için UPDATE/DELETE, tablo oluşturulduğunda DENY ile kapatılacak (append-only).
