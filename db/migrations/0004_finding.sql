-- Bulgular: bir bileşenle bir zafiyetin eşleşmesi ve bu eşleşmenin yaşam döngüsü.

CREATE TABLE finding.Findings
(
    Id                bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Findings PRIMARY KEY,
    ComponentId       bigint               NOT NULL
        CONSTRAINT FK_Findings_Components REFERENCES inventory.Components (Id) ON DELETE CASCADE,
    VulnerabilityId   int                  NOT NULL
        CONSTRAINT FK_Findings_Vulnerabilities REFERENCES vuln.Vulnerabilities (Id),
    Status            tinyint              NOT NULL
        CONSTRAINT CK_Findings_Status CHECK (Status BETWEEN 1 AND 5), -- 1 Open, 2 InReview, 3 Fixed, 4 RiskAccepted, 5 FalsePositive
    MatchConfidence   tinyint              NOT NULL
        CONSTRAINT CK_Findings_MatchConfidence CHECK (MatchConfidence BETWEEN 1 AND 4), -- 1 Low, 2 Medium, 3 High, 4 Exact
    RiskScore         decimal(5,2)         NOT NULL
        CONSTRAINT CK_Findings_RiskScore CHECK (RiskScore BETWEEN 0 AND 100),
    Priority          tinyint              NOT NULL
        CONSTRAINT CK_Findings_Priority CHECK (Priority BETWEEN 1 AND 4), -- 1 Now, 2 ThisWeek, 3 Plan, 4 Watch
    FirstSeenAt       datetimeoffset(3)    NOT NULL CONSTRAINT DF_Findings_FirstSeenAt DEFAULT SYSDATETIMEOFFSET(),
    LastSeenAt        datetimeoffset(3)    NOT NULL CONSTRAINT DF_Findings_LastSeenAt DEFAULT SYSDATETIMEOFFSET(),
    ResolvedAt        datetimeoffset(3)    NULL,
    RiskAcceptedUntil date                 NULL,
    AssignedToUserId  nvarchar(450)        NULL,
    RowVersion        rowversion           NOT NULL,
    CONSTRAINT UQ_Findings_Component_Vulnerability UNIQUE (ComponentId, VulnerabilityId),
    -- Risk kabulü süresiz olamaz: tarih zorunlu, süresi dolunca bulgu yeniden açılır.
    CONSTRAINT CK_Findings_RiskAcceptedUntil CHECK (Status <> 4 OR RiskAcceptedUntil IS NOT NULL)
);
CREATE INDEX IX_Findings_Status_Priority ON finding.Findings (Status, Priority)
    INCLUDE (ComponentId, VulnerabilityId, RiskScore);
CREATE INDEX IX_Findings_VulnerabilityId ON finding.Findings (VulnerabilityId);

CREATE TABLE finding.FindingHistory
(
    Id              bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FindingHistory PRIMARY KEY,
    FindingId       bigint               NOT NULL
        CONSTRAINT FK_FindingHistory_Findings REFERENCES finding.Findings (Id) ON DELETE CASCADE,
    FromStatus      tinyint              NULL,
    ToStatus        tinyint              NOT NULL,
    Comment         nvarchar(1000)       NULL,
    ChangedByUserId nvarchar(450)        NULL, -- NULL = sistem (Worker)
    ChangedAt       datetimeoffset(3)    NOT NULL CONSTRAINT DF_FindingHistory_ChangedAt DEFAULT SYSDATETIMEOFFSET()
);
CREATE INDEX IX_FindingHistory_FindingId ON finding.FindingHistory (FindingId);
