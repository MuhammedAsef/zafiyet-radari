-- Envanter modülü: varlık grupları, üyelikler, varlıklar, yüklemeler, bileşenler.
-- Enum kolonları tinyint + CHECK; değerler Domain'deki enum'larla birebir aynıdır.

CREATE TABLE inventory.AssetGroups
(
    Id          int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AssetGroups PRIMARY KEY,
    Name        nvarchar(100)     NOT NULL,
    Description nvarchar(500)     NULL,
    CreatedBy   nvarchar(450)     NOT NULL,
    CreatedAt   datetimeoffset(3) NOT NULL CONSTRAINT DF_AssetGroups_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    RowVersion  rowversion        NOT NULL
);

-- Erişim üyelikle belirlenir. Her sorgu kullanıcının üyeliğiyle filtrelenir (IDOR önlemi).
CREATE TABLE inventory.AssetGroupMembers
(
    AssetGroupId int               NOT NULL
        CONSTRAINT FK_AssetGroupMembers_AssetGroups REFERENCES inventory.AssetGroups (Id) ON DELETE CASCADE,
    UserId       nvarchar(450)     NOT NULL,
    Role         tinyint           NOT NULL
        CONSTRAINT CK_AssetGroupMembers_Role CHECK (Role BETWEEN 1 AND 3), -- 1 Owner, 2 Editor, 3 Viewer
    AddedAt      datetimeoffset(3) NOT NULL CONSTRAINT DF_AssetGroupMembers_AddedAt DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT PK_AssetGroupMembers PRIMARY KEY (AssetGroupId, UserId)
);
CREATE INDEX IX_AssetGroupMembers_UserId ON inventory.AssetGroupMembers (UserId);

CREATE TABLE inventory.Assets
(
    Id               int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Assets PRIMARY KEY,
    AssetGroupId     int               NOT NULL
        CONSTRAINT FK_Assets_AssetGroups REFERENCES inventory.AssetGroups (Id) ON DELETE CASCADE,
    Name             nvarchar(200)     NOT NULL,
    Environment      tinyint           NOT NULL
        CONSTRAINT CK_Assets_Environment CHECK (Environment BETWEEN 1 AND 3), -- 1 Development, 2 Test, 3 Production
    Criticality      tinyint           NOT NULL
        CONSTRAINT CK_Assets_Criticality CHECK (Criticality BETWEEN 1 AND 4), -- 1 Low, 2 Medium, 3 High, 4 Critical
    IsInternetFacing bit               NOT NULL CONSTRAINT DF_Assets_IsInternetFacing DEFAULT 0,
    CreatedAt        datetimeoffset(3) NOT NULL CONSTRAINT DF_Assets_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt        datetimeoffset(3) NULL,
    RowVersion       rowversion        NOT NULL,
    CONSTRAINT UQ_Assets_AssetGroup_Name UNIQUE (AssetGroupId, Name)
);

CREATE TABLE inventory.InventoryUploads
(
    Id               int IDENTITY(1,1) NOT NULL CONSTRAINT PK_InventoryUploads PRIMARY KEY,
    AssetId          int               NOT NULL
        CONSTRAINT FK_InventoryUploads_Assets REFERENCES inventory.Assets (Id) ON DELETE CASCADE,
    OriginalFileName nvarchar(255)     NOT NULL, -- sadece gösterim için; dosya diske bu adla yazılmaz
    Format           tinyint           NOT NULL
        CONSTRAINT CK_InventoryUploads_Format CHECK (Format BETWEEN 1 AND 3), -- 1 CycloneDx, 2 NpmLock, 3 NuGetLock
    Sha256           binary(32)        NOT NULL, -- bütünlük + aynı dosyanın tekrar yüklenmesini tespit
    SizeBytes        int               NOT NULL CONSTRAINT CK_InventoryUploads_SizeBytes CHECK (SizeBytes > 0),
    ComponentCount   int               NOT NULL CONSTRAINT CK_InventoryUploads_ComponentCount CHECK (ComponentCount >= 0),
    UploadedBy       nvarchar(450)     NOT NULL,
    UploadedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_InventoryUploads_UploadedAt DEFAULT SYSDATETIMEOFFSET()
);
CREATE INDEX IX_InventoryUploads_AssetId_Sha256 ON inventory.InventoryUploads (AssetId, Sha256);

CREATE TABLE inventory.Components
(
    Id        bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Components PRIMARY KEY,
    AssetId   int                  NOT NULL
        CONSTRAINT FK_Components_Assets REFERENCES inventory.Assets (Id) ON DELETE CASCADE,
    Ecosystem varchar(20)          NOT NULL
        CONSTRAINT CK_Components_Ecosystem CHECK (Ecosystem IN ('npm', 'NuGet')), -- OSV ekosistem adları
    Name      nvarchar(400)        NOT NULL,
    Version   nvarchar(100)        NOT NULL,
    Purl      nvarchar(1000)       NULL,
    Source    tinyint              NOT NULL
        CONSTRAINT CK_Components_Source CHECK (Source BETWEEN 1 AND 2), -- 1 Manual, 2 Upload
    UploadId  int                  NULL
        CONSTRAINT FK_Components_InventoryUploads REFERENCES inventory.InventoryUploads (Id),
    IsActive  bit                  NOT NULL CONSTRAINT DF_Components_IsActive DEFAULT 1, -- yeni yüklemede yoksa pasife alınır
    CreatedAt datetimeoffset(3)    NOT NULL CONSTRAINT DF_Components_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT UQ_Components_Asset_Package UNIQUE (AssetId, Ecosystem, Name, Version)
);
-- Eşleştirme motorunun ana sorgusu: "bu pakete sahip aktif bileşenler hangileri?"
CREATE INDEX IX_Components_Ecosystem_Name ON inventory.Components (Ecosystem, Name)
    INCLUDE (Version, AssetId) WHERE IsActive = 1;
