-- Zafiyet modülü: OSV, CISA KEV ve EPSS verilerinin birleştiği yer.
-- Dış kaynaktan gelen her alan güvenilmez girdi kabul edilir: uzunluklar sınırlı, arayüzde encode edilerek gösterilir.

CREATE TABLE vuln.Vulnerabilities
(
    Id             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Vulnerabilities PRIMARY KEY,
    ExternalId     varchar(64)       NOT NULL CONSTRAINT UQ_Vulnerabilities_ExternalId UNIQUE, -- GHSA-..., CVE-...
    Source         varchar(20)       NOT NULL, -- OSV, NVD
    Summary        nvarchar(500)     NULL,
    Details        nvarchar(max)     NULL,
    PublishedAt    datetimeoffset(3) NULL,
    ModifiedAt     datetimeoffset(3) NULL,
    WithdrawnAt    datetimeoffset(3) NULL,
    CvssScore      decimal(3,1)      NULL CONSTRAINT CK_Vulnerabilities_CvssScore CHECK (CvssScore BETWEEN 0 AND 10),
    CvssVector     varchar(200)      NULL,
    EpssScore      decimal(6,5)      NULL CONSTRAINT CK_Vulnerabilities_EpssScore CHECK (EpssScore BETWEEN 0 AND 1),
    EpssPercentile decimal(6,5)      NULL CONSTRAINT CK_Vulnerabilities_EpssPercentile CHECK (EpssPercentile BETWEEN 0 AND 1),
    EpssUpdatedAt  date              NULL,
    IsKev          bit               NOT NULL CONSTRAINT DF_Vulnerabilities_IsKev DEFAULT 0,
    KevAddedAt     date              NULL,
    KevDueDate     date              NULL,
    KevRansomware  bit               NULL,
    SyncedAt       datetimeoffset(3) NOT NULL CONSTRAINT DF_Vulnerabilities_SyncedAt DEFAULT SYSDATETIMEOFFSET()
);
CREATE INDEX IX_Vulnerabilities_IsKev ON vuln.Vulnerabilities (IsKev) WHERE IsKev = 1;

-- KEV ve EPSS CVE kimliğiyle gelir; OSV kaydı GHSA olabilir. Eşleme alias'lar üzerinden yapılır.
CREATE TABLE vuln.VulnerabilityAliases
(
    VulnerabilityId int         NOT NULL
        CONSTRAINT FK_VulnerabilityAliases_Vulnerabilities REFERENCES vuln.Vulnerabilities (Id) ON DELETE CASCADE,
    Alias           varchar(64) NOT NULL,
    CONSTRAINT PK_VulnerabilityAliases PRIMARY KEY (VulnerabilityId, Alias)
);
CREATE INDEX IX_VulnerabilityAliases_Alias ON vuln.VulnerabilityAliases (Alias);

CREATE TABLE vuln.AffectedPackages
(
    Id              bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AffectedPackages PRIMARY KEY,
    VulnerabilityId int                  NOT NULL
        CONSTRAINT FK_AffectedPackages_Vulnerabilities REFERENCES vuln.Vulnerabilities (Id) ON DELETE CASCADE,
    Ecosystem       varchar(20)          NOT NULL,
    PackageName     nvarchar(400)        NOT NULL,
    Purl            nvarchar(1000)       NULL
);
-- Eşleştirme: Components (Ecosystem, Name) ile birleşir.
CREATE INDEX IX_AffectedPackages_Ecosystem_PackageName ON vuln.AffectedPackages (Ecosystem, PackageName)
    INCLUDE (VulnerabilityId);

-- OSV "events" listesinin introduced/fixed çiftlerine açılmış hali.
CREATE TABLE vuln.AffectedRanges
(
    Id                bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AffectedRanges PRIMARY KEY,
    AffectedPackageId bigint               NOT NULL
        CONSTRAINT FK_AffectedRanges_AffectedPackages REFERENCES vuln.AffectedPackages (Id) ON DELETE CASCADE,
    RangeType         tinyint              NOT NULL
        CONSTRAINT CK_AffectedRanges_RangeType CHECK (RangeType BETWEEN 1 AND 2), -- 1 Semver, 2 Ecosystem
    Introduced        nvarchar(100)        NULL,
    Fixed             nvarchar(100)        NULL,
    LastAffected      nvarchar(100)        NULL,
    CONSTRAINT CK_AffectedRanges_Bounds CHECK (Fixed IS NULL OR LastAffected IS NULL)
);
CREATE INDEX IX_AffectedRanges_AffectedPackageId ON vuln.AffectedRanges (AffectedPackageId);

CREATE TABLE vuln.VulnerabilityReferences
(
    Id              bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_VulnerabilityReferences PRIMARY KEY,
    VulnerabilityId int                  NOT NULL
        CONSTRAINT FK_VulnerabilityReferences_Vulnerabilities REFERENCES vuln.Vulnerabilities (Id) ON DELETE CASCADE,
    Type            varchar(30)          NOT NULL, -- ADVISORY, FIX, PACKAGE, WEB ...
    Url             nvarchar(2000)       NOT NULL  -- arayüzde sadece http/https şemasıyla link verilir
);
CREATE INDEX IX_VulnerabilityReferences_VulnerabilityId ON vuln.VulnerabilityReferences (VulnerabilityId);
