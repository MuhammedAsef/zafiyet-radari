-- Bildirim kanalları, kuralları ve gönderim kayıtları.

CREATE TABLE notify.NotificationChannels
(
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_NotificationChannels PRIMARY KEY,
    AssetGroupId    int               NOT NULL
        CONSTRAINT FK_NotificationChannels_AssetGroups REFERENCES inventory.AssetGroups (Id) ON DELETE CASCADE,
    Type            tinyint           NOT NULL
        CONSTRAINT CK_NotificationChannels_Type CHECK (Type BETWEEN 1 AND 3), -- 1 Email, 2 Webhook, 3 Telegram
    Name            nvarchar(100)     NOT NULL,
    Target          nvarchar(500)     NOT NULL, -- e-posta adresi veya https URL (SSRF kontrolünden geçer)
    ProtectedSecret nvarchar(max)     NULL,     -- webhook secret / bot token: Data Protection ile şifreli, düz metin asla
    IsEnabled       bit               NOT NULL CONSTRAINT DF_NotificationChannels_IsEnabled DEFAULT 1,
    CreatedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_NotificationChannels_CreatedAt DEFAULT SYSDATETIMEOFFSET()
);
CREATE INDEX IX_NotificationChannels_AssetGroupId ON notify.NotificationChannels (AssetGroupId);

CREATE TABLE notify.NotificationRules
(
    Id          int IDENTITY(1,1) NOT NULL CONSTRAINT PK_NotificationRules PRIMARY KEY,
    ChannelId   int               NOT NULL
        CONSTRAINT FK_NotificationRules_NotificationChannels REFERENCES notify.NotificationChannels (Id) ON DELETE CASCADE,
    MinPriority tinyint           NOT NULL
        CONSTRAINT CK_NotificationRules_MinPriority CHECK (MinPriority BETWEEN 1 AND 4), -- bu öncelik ve üstü bildirilir
    OnlyKev     bit               NOT NULL CONSTRAINT DF_NotificationRules_OnlyKev DEFAULT 0,
    IsEnabled   bit               NOT NULL CONSTRAINT DF_NotificationRules_IsEnabled DEFAULT 1
);
CREATE INDEX IX_NotificationRules_ChannelId ON notify.NotificationRules (ChannelId);

CREATE TABLE notify.NotificationLog
(
    Id        bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_NotificationLog PRIMARY KEY,
    ChannelId int                  NOT NULL
        CONSTRAINT FK_NotificationLog_NotificationChannels REFERENCES notify.NotificationChannels (Id) ON DELETE CASCADE,
    FindingId bigint               NULL, -- bilinçli olarak FK yok: kayıt, bulgu silinse de iz olarak kalır
    Status    tinyint              NOT NULL
        CONSTRAINT CK_NotificationLog_Status CHECK (Status BETWEEN 1 AND 2), -- 1 Sent, 2 Failed
    Error     nvarchar(500)        NULL,
    CreatedAt datetimeoffset(3)    NOT NULL CONSTRAINT DF_NotificationLog_CreatedAt DEFAULT SYSDATETIMEOFFSET()
);
CREATE INDEX IX_NotificationLog_ChannelId_CreatedAt ON notify.NotificationLog (ChannelId, CreatedAt);
