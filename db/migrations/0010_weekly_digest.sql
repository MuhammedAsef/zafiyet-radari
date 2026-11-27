-- Haftalık özet maili: kanal bazında açılıp kapatılabilir.
-- Varsayılan 0 (CLR'deki bool varsayılanıyla aynı): EF, false değerini "atanmamış" sanıp
-- veritabanı varsayılanını yazmasın. Yeni kanallarda değeri uygulama açıkça verir.
-- Mevcut kanallar özete dahil edilir (aşağıdaki UPDATE).

ALTER TABLE notify.NotificationChannels
    ADD WeeklyDigest bit NOT NULL CONSTRAINT DF_NotificationChannels_WeeklyDigest DEFAULT 0;
GO

UPDATE notify.NotificationChannels SET WeeklyDigest = 1;
GO
