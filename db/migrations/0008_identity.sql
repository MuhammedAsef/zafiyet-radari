-- ASP.NET Core Identity tabloları (auth şeması). Kolonlar IdentityDbContext'in varsayılan modeliyle aynıdır.
-- Şifreler PBKDF2 (Identity PasswordHasher) ile hash'lenir; düz metin şifre hiçbir yerde tutulmaz.

CREATE TABLE auth.AspNetUsers
(
    Id                   nvarchar(450)  NOT NULL CONSTRAINT PK_AspNetUsers PRIMARY KEY,
    UserName             nvarchar(256)  NULL,
    NormalizedUserName   nvarchar(256)  NULL,
    Email                nvarchar(256)  NULL,
    NormalizedEmail      nvarchar(256)  NULL,
    EmailConfirmed       bit            NOT NULL,
    PasswordHash         nvarchar(max)  NULL,
    SecurityStamp        nvarchar(max)  NULL,
    ConcurrencyStamp     nvarchar(max)  NULL,
    PhoneNumber          nvarchar(max)  NULL,
    PhoneNumberConfirmed bit            NOT NULL,
    TwoFactorEnabled     bit            NOT NULL,
    LockoutEnd           datetimeoffset NULL,
    LockoutEnabled       bit            NOT NULL,
    AccessFailedCount    int            NOT NULL
);
CREATE UNIQUE INDEX UserNameIndex ON auth.AspNetUsers (NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
CREATE UNIQUE INDEX EmailIndex ON auth.AspNetUsers (NormalizedEmail) WHERE NormalizedEmail IS NOT NULL;

CREATE TABLE auth.AspNetRoles
(
    Id               nvarchar(450) NOT NULL CONSTRAINT PK_AspNetRoles PRIMARY KEY,
    Name             nvarchar(256) NULL,
    NormalizedName   nvarchar(256) NULL,
    ConcurrencyStamp nvarchar(max) NULL
);
CREATE UNIQUE INDEX RoleNameIndex ON auth.AspNetRoles (NormalizedName) WHERE NormalizedName IS NOT NULL;

CREATE TABLE auth.AspNetRoleClaims
(
    Id         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AspNetRoleClaims PRIMARY KEY,
    RoleId     nvarchar(450)     NOT NULL
        CONSTRAINT FK_AspNetRoleClaims_AspNetRoles REFERENCES auth.AspNetRoles (Id) ON DELETE CASCADE,
    ClaimType  nvarchar(max)     NULL,
    ClaimValue nvarchar(max)     NULL
);
CREATE INDEX IX_AspNetRoleClaims_RoleId ON auth.AspNetRoleClaims (RoleId);

CREATE TABLE auth.AspNetUserClaims
(
    Id         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AspNetUserClaims PRIMARY KEY,
    UserId     nvarchar(450)     NOT NULL
        CONSTRAINT FK_AspNetUserClaims_AspNetUsers REFERENCES auth.AspNetUsers (Id) ON DELETE CASCADE,
    ClaimType  nvarchar(max)     NULL,
    ClaimValue nvarchar(max)     NULL
);
CREATE INDEX IX_AspNetUserClaims_UserId ON auth.AspNetUserClaims (UserId);

CREATE TABLE auth.AspNetUserLogins
(
    LoginProvider       nvarchar(450) NOT NULL,
    ProviderKey         nvarchar(450) NOT NULL,
    ProviderDisplayName nvarchar(max) NULL,
    UserId              nvarchar(450) NOT NULL
        CONSTRAINT FK_AspNetUserLogins_AspNetUsers REFERENCES auth.AspNetUsers (Id) ON DELETE CASCADE,
    CONSTRAINT PK_AspNetUserLogins PRIMARY KEY (LoginProvider, ProviderKey)
);
CREATE INDEX IX_AspNetUserLogins_UserId ON auth.AspNetUserLogins (UserId);

CREATE TABLE auth.AspNetUserRoles
(
    UserId nvarchar(450) NOT NULL
        CONSTRAINT FK_AspNetUserRoles_AspNetUsers REFERENCES auth.AspNetUsers (Id) ON DELETE CASCADE,
    RoleId nvarchar(450) NOT NULL
        CONSTRAINT FK_AspNetUserRoles_AspNetRoles REFERENCES auth.AspNetRoles (Id) ON DELETE CASCADE,
    CONSTRAINT PK_AspNetUserRoles PRIMARY KEY (UserId, RoleId)
);
CREATE INDEX IX_AspNetUserRoles_RoleId ON auth.AspNetUserRoles (RoleId);

CREATE TABLE auth.AspNetUserTokens
(
    UserId        nvarchar(450) NOT NULL
        CONSTRAINT FK_AspNetUserTokens_AspNetUsers REFERENCES auth.AspNetUsers (Id) ON DELETE CASCADE,
    LoginProvider nvarchar(450) NOT NULL,
    Name          nvarchar(450) NOT NULL,
    Value         nvarchar(max) NULL,
    CONSTRAINT PK_AspNetUserTokens PRIMARY KEY (UserId, LoginProvider, Name)
);

-- Sistem rolleri. Kayıt olan herkes Analyst olur; Admin sadece Migrator CLI ile verilir (grant-admin).
INSERT INTO auth.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp) VALUES
    (N'6f1c2a3e-8b1d-4c7a-9e2f-1a0b3c4d5e01', N'Admin',   N'ADMIN',   CONVERT(nvarchar(36), NEWID())),
    (N'6f1c2a3e-8b1d-4c7a-9e2f-1a0b3c4d5e02', N'Analyst', N'ANALYST', CONVERT(nvarchar(36), NEWID())),
    (N'6f1c2a3e-8b1d-4c7a-9e2f-1a0b3c4d5e03', N'Viewer',  N'VIEWER',  CONVERT(nvarchar(36), NEWID()));

-- Varlık grubu üyeliği gerçek kullanıcılara bağlı; kullanıcı silinince üyelikleri de silinir.
ALTER TABLE inventory.AssetGroupMembers
    ADD CONSTRAINT FK_AssetGroupMembers_AspNetUsers
        FOREIGN KEY (UserId) REFERENCES auth.AspNetUsers (Id) ON DELETE CASCADE;
