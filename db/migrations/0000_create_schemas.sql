-- Modül bazlı şemalar. Tablolar sonraki script'lerde gelir.
IF SCHEMA_ID(N'inventory') IS NULL EXEC (N'CREATE SCHEMA inventory');
IF SCHEMA_ID(N'vuln')      IS NULL EXEC (N'CREATE SCHEMA vuln');
IF SCHEMA_ID(N'finding')   IS NULL EXEC (N'CREATE SCHEMA finding');
IF SCHEMA_ID(N'notify')    IS NULL EXEC (N'CREATE SCHEMA notify');
IF SCHEMA_ID(N'ops')       IS NULL EXEC (N'CREATE SCHEMA ops');
IF SCHEMA_ID(N'auth')      IS NULL EXEC (N'CREATE SCHEMA auth');
