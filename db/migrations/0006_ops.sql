-- Operasyon şeması: veri kaynağı senkronizasyon durumu, Data Protection anahtarları,
-- hash zincirli (değiştirilemez) audit log.

CREATE TABLE ops.FeedSyncState
(
    Source         varchar(30)       NOT NULL CONSTRAINT PK_FeedSyncState PRIMARY KEY, -- OSV-npm, OSV-NuGet, KEV, EPSS
    LastAttemptAt  datetimeoffset(3) NULL,
    LastSuccessAt  datetimeoffset(3) NULL,
    SyncCursor     nvarchar(200)     NULL, -- artımlı senkronizasyon: son değişiklik zamanı / ETag
    ItemsProcessed int               NOT NULL CONSTRAINT DF_FeedSyncState_ItemsProcessed DEFAULT 0,
    LastError      nvarchar(1000)    NULL
);

-- ASP.NET Core Data Protection anahtar halkası (cookie, antiforgery ve kanal sırlarının şifrelenmesi).
-- Kolon adları Microsoft.AspNetCore.DataProtection.EntityFrameworkCore'un beklediği şekildedir.
CREATE TABLE ops.DataProtectionKeys
(
    Id           int IDENTITY(1,1) NOT NULL CONSTRAINT PK_DataProtectionKeys PRIMARY KEY,
    FriendlyName nvarchar(max)     NULL,
    Xml          nvarchar(max)     NULL
);

CREATE TABLE ops.AuditLog
(
    Id           bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
    OccurredAt   datetimeoffset(3)    NOT NULL,
    EventType    varchar(50)          NOT NULL, -- Auth.LoginFailed, Finding.RiskAccepted, ...
    UserId       nvarchar(450)        NULL,
    UserName     nvarchar(256)        NULL,
    IpAddress    varchar(45)          NULL,
    TargetType   varchar(50)          NULL,
    TargetId     nvarchar(100)        NULL,
    Details      nvarchar(2000)       NULL, -- şifre, token, secret asla yazılmaz
    PreviousHash binary(32)           NOT NULL,
    Hash         binary(32)           NOT NULL  -- SHA-256(PreviousHash + kayıt içeriği)
);
CREATE INDEX IX_AuditLog_OccurredAt ON ops.AuditLog (OccurredAt);
CREATE INDEX IX_AuditLog_EventType ON ops.AuditLog (EventType, OccurredAt);
GO

-- Hash'e giren içeriğin tek tanımı. Ekleme ve doğrulama aynı fonksiyonu kullanır.
CREATE FUNCTION ops.fn_AuditPayload
(
    @OccurredAt datetimeoffset(3),
    @EventType  varchar(50),
    @UserId     nvarchar(450),
    @UserName   nvarchar(256),
    @IpAddress  varchar(45),
    @TargetType varchar(50),
    @TargetId   nvarchar(100),
    @Details    nvarchar(2000)
)
RETURNS varbinary(max)
WITH SCHEMABINDING
AS
BEGIN
    DECLARE @Separator nchar(1) = NCHAR(31); -- ASCII unit separator: alan sınırları belirsiz olamaz
    DECLARE @Null nvarchar(6) = N'<null>';

    RETURN CAST(CONCAT(
        CONVERT(nvarchar(40), @OccurredAt, 127), @Separator,
        @EventType,                              @Separator,
        COALESCE(@UserId, @Null),                @Separator,
        COALESCE(@UserName, @Null),              @Separator,
        COALESCE(CAST(@IpAddress AS nvarchar(45)), @Null),  @Separator,
        COALESCE(CAST(@TargetType AS nvarchar(50)), @Null), @Separator,
        COALESCE(@TargetId, @Null),              @Separator,
        COALESCE(@Details, @Null)
    ) AS varbinary(max));
END
GO

-- Audit kaydı eklemenin tek yolu. Uygulama tabloya doğrudan INSERT yapamaz (aşağıdaki DENY).
CREATE PROCEDURE ops.usp_AppendAuditLog
    @EventType  varchar(50),
    @UserId     nvarchar(450)  = NULL,
    @UserName   nvarchar(256)  = NULL,
    @IpAddress  varchar(45)    = NULL,
    @TargetType varchar(50)    = NULL,
    @TargetId   nvarchar(100)  = NULL,
    @Details    nvarchar(2000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @OccurredAt datetimeoffset(3) = SYSDATETIMEOFFSET();
    DECLARE @PreviousHash binary(32);

    BEGIN TRANSACTION;

    -- Zincirin sonuna aynı anda iki kayıt eklenemez: yazmalar sıraya girer.
    EXEC sys.sp_getapplock @Resource = N'ops.AuditLog', @LockMode = 'Exclusive', @LockOwner = 'Transaction';

    SELECT TOP (1) @PreviousHash = Hash FROM ops.AuditLog ORDER BY Id DESC;
    SET @PreviousHash = ISNULL(@PreviousHash, CAST(0x00 AS binary(32)));

    INSERT INTO ops.AuditLog
        (OccurredAt, EventType, UserId, UserName, IpAddress, TargetType, TargetId, Details, PreviousHash, Hash)
    VALUES
        (@OccurredAt, @EventType, @UserId, @UserName, @IpAddress, @TargetType, @TargetId, @Details, @PreviousHash,
         HASHBYTES('SHA2_256', @PreviousHash + ops.fn_AuditPayload(
             @OccurredAt, @EventType, @UserId, @UserName, @IpAddress, @TargetType, @TargetId, @Details)));

    COMMIT TRANSACTION;
END
GO

-- Zinciri baştan sona doğrular. Değiştirilmiş veya aradan silinmiş ilk kaydın Id'sini döner; sağlamsa boş sonuç.
CREATE PROCEDURE ops.usp_VerifyAuditChain
AS
BEGIN
    SET NOCOUNT ON;

    WITH Chain AS
    (
        SELECT
            Id,
            PreviousHash,
            Hash,
            LAG(Hash, 1, CAST(0x00 AS binary(32))) OVER (ORDER BY Id) AS ExpectedPreviousHash,
            HASHBYTES('SHA2_256', PreviousHash + ops.fn_AuditPayload(
                OccurredAt, EventType, UserId, UserName, IpAddress, TargetType, TargetId, Details)) AS ExpectedHash
        FROM ops.AuditLog
    )
    SELECT TOP (1) Id AS FirstBrokenId
    FROM Chain
    WHERE PreviousHash <> ExpectedPreviousHash OR Hash <> ExpectedHash
    ORDER BY Id;
END
GO

-- Append-only: uygulama kullanıcıları kaydı değiştiremez, silemez, doğrudan ekleyemez.
-- Prosedür dbo sahipliğinde olduğu için (ownership chaining) ekleme sadece prosedür üzerinden çalışır.
DENY INSERT, UPDATE, DELETE ON ops.AuditLog TO zr_web_role;
DENY INSERT, UPDATE, DELETE ON ops.AuditLog TO zr_worker_role;
GRANT EXECUTE ON ops.usp_AppendAuditLog TO zr_web_role;
GRANT EXECUTE ON ops.usp_AppendAuditLog TO zr_worker_role;
GRANT EXECUTE ON ops.usp_VerifyAuditChain TO zr_web_role;
