using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using ZafiyetRadari.Domain.Entities;

namespace ZafiyetRadari.Infrastructure.Persistence;

public partial class ZafiyetRadariDbContext : DbContext
{
    public ZafiyetRadariDbContext(DbContextOptions<ZafiyetRadariDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AffectedPackage> AffectedPackages { get; set; }

    public virtual DbSet<AffectedRange> AffectedRanges { get; set; }

    public virtual DbSet<Asset> Assets { get; set; }

    public virtual DbSet<AssetGroup> AssetGroups { get; set; }

    public virtual DbSet<AssetGroupMember> AssetGroupMembers { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Component> Components { get; set; }

    public virtual DbSet<FeedSyncState> FeedSyncStates { get; set; }

    public virtual DbSet<Finding> Findings { get; set; }

    public virtual DbSet<FindingHistory> FindingHistories { get; set; }

    public virtual DbSet<InventoryUpload> InventoryUploads { get; set; }

    public virtual DbSet<NotificationChannel> NotificationChannels { get; set; }

    public virtual DbSet<NotificationLog> NotificationLogs { get; set; }

    public virtual DbSet<NotificationRule> NotificationRules { get; set; }

    public virtual DbSet<Vulnerability> Vulnerabilities { get; set; }

    public virtual DbSet<VulnerabilityAlias> VulnerabilityAliases { get; set; }

    public virtual DbSet<VulnerabilityReference> VulnerabilityReferences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AffectedPackage>(entity =>
        {
            entity.ToTable("AffectedPackages", "vuln");

            entity.HasIndex(e => new { e.Ecosystem, e.PackageName }, "IX_AffectedPackages_Ecosystem_PackageName");

            entity.Property(e => e.Ecosystem)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.PackageName).HasMaxLength(400);
            entity.Property(e => e.Purl).HasMaxLength(1000);

            entity.HasOne(d => d.Vulnerability).WithMany(p => p.AffectedPackages)
                .HasForeignKey(d => d.VulnerabilityId)
                .HasConstraintName("FK_AffectedPackages_Vulnerabilities");
        });

        modelBuilder.Entity<AffectedRange>(entity =>
        {
            entity.ToTable("AffectedRanges", "vuln");

            entity.HasIndex(e => e.AffectedPackageId, "IX_AffectedRanges_AffectedPackageId");

            entity.Property(e => e.Fixed).HasMaxLength(100);
            entity.Property(e => e.Introduced).HasMaxLength(100);
            entity.Property(e => e.LastAffected).HasMaxLength(100);

            entity.HasOne(d => d.AffectedPackage).WithMany(p => p.AffectedRanges)
                .HasForeignKey(d => d.AffectedPackageId)
                .HasConstraintName("FK_AffectedRanges_AffectedPackages");
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.ToTable("Assets", "inventory");

            entity.HasIndex(e => new { e.AssetGroupId, e.Name }, "UQ_Assets_AssetGroup_Name").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysdatetimeoffset())", "DF_Assets_CreatedAt");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasOne(d => d.AssetGroup).WithMany(p => p.Assets)
                .HasForeignKey(d => d.AssetGroupId)
                .HasConstraintName("FK_Assets_AssetGroups");
        });

        modelBuilder.Entity<AssetGroup>(entity =>
        {
            entity.ToTable("AssetGroups", "inventory");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysdatetimeoffset())", "DF_AssetGroups_CreatedAt");
            entity.Property(e => e.CreatedBy).HasMaxLength(450);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<AssetGroupMember>(entity =>
        {
            entity.HasKey(e => new { e.AssetGroupId, e.UserId });

            entity.ToTable("AssetGroupMembers", "inventory");

            entity.HasIndex(e => e.UserId, "IX_AssetGroupMembers_UserId");

            entity.Property(e => e.AddedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysdatetimeoffset())", "DF_AssetGroupMembers_AddedAt");

            entity.HasOne(d => d.AssetGroup).WithMany(p => p.AssetGroupMembers)
                .HasForeignKey(d => d.AssetGroupId)
                .HasConstraintName("FK_AssetGroupMembers_AssetGroups");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLog", "ops");

            entity.HasIndex(e => new { e.EventType, e.OccurredAt }, "IX_AuditLog_EventType");

            entity.HasIndex(e => e.OccurredAt, "IX_AuditLog_OccurredAt");

            entity.Property(e => e.Details).HasMaxLength(2000);
            entity.Property(e => e.EventType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Hash)
                .HasMaxLength(32)
                .IsFixedLength();
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .IsUnicode(false);
            entity.Property(e => e.OccurredAt).HasPrecision(3);
            entity.Property(e => e.PreviousHash)
                .HasMaxLength(32)
                .IsFixedLength();
            entity.Property(e => e.TargetId).HasMaxLength(100);
            entity.Property(e => e.TargetType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.UserName).HasMaxLength(256);
        });

        modelBuilder.Entity<Component>(entity =>
        {
            entity.ToTable("Components", "inventory");

            entity.HasIndex(e => new { e.Ecosystem, e.Name }, "IX_Components_Ecosystem_Name").HasFilter("([IsActive]=(1))");

            entity.HasIndex(e => new { e.AssetId, e.Ecosystem, e.Name, e.Version }, "UQ_Components_Asset_Package").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysdatetimeoffset())", "DF_Components_CreatedAt");
            entity.Property(e => e.Ecosystem)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Components_IsActive");
            entity.Property(e => e.Name).HasMaxLength(400);
            entity.Property(e => e.Purl).HasMaxLength(1000);
            entity.Property(e => e.Version).HasMaxLength(100);

            entity.HasOne(d => d.Asset).WithMany(p => p.Components)
                .HasForeignKey(d => d.AssetId)
                .HasConstraintName("FK_Components_Assets");

            entity.HasOne(d => d.Upload).WithMany(p => p.Components)
                .HasForeignKey(d => d.UploadId)
                .HasConstraintName("FK_Components_InventoryUploads");
        });

        modelBuilder.Entity<FeedSyncState>(entity =>
        {
            entity.HasKey(e => e.Source);

            entity.ToTable("FeedSyncState", "ops");

            entity.Property(e => e.Source)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.LastAttemptAt).HasPrecision(3);
            entity.Property(e => e.LastError).HasMaxLength(1000);
            entity.Property(e => e.LastSuccessAt).HasPrecision(3);
            entity.Property(e => e.SyncCursor).HasMaxLength(200);
        });

        modelBuilder.Entity<Finding>(entity =>
        {
            entity.ToTable("Findings", "finding");

            entity.HasIndex(e => new { e.Status, e.Priority }, "IX_Findings_Status_Priority");

            entity.HasIndex(e => e.VulnerabilityId, "IX_Findings_VulnerabilityId");

            entity.HasIndex(e => new { e.ComponentId, e.VulnerabilityId }, "UQ_Findings_Component_Vulnerability").IsUnique();

            entity.Property(e => e.AssignedToUserId).HasMaxLength(450);
            entity.Property(e => e.FirstSeenAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysdatetimeoffset())", "DF_Findings_FirstSeenAt");
            entity.Property(e => e.LastSeenAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysdatetimeoffset())", "DF_Findings_LastSeenAt");
            entity.Property(e => e.ResolvedAt).HasPrecision(3);
            entity.Property(e => e.RiskScore).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.Component).WithMany(p => p.Findings)
                .HasForeignKey(d => d.ComponentId)
                .HasConstraintName("FK_Findings_Components");

            entity.HasOne(d => d.Vulnerability).WithMany(p => p.Findings)
                .HasForeignKey(d => d.VulnerabilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Findings_Vulnerabilities");
        });

        modelBuilder.Entity<FindingHistory>(entity =>
        {
            entity.ToTable("FindingHistory", "finding");

            entity.HasIndex(e => e.FindingId, "IX_FindingHistory_FindingId");

            entity.Property(e => e.ChangedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysdatetimeoffset())", "DF_FindingHistory_ChangedAt");
            entity.Property(e => e.ChangedByUserId).HasMaxLength(450);
            entity.Property(e => e.Comment).HasMaxLength(1000);

            entity.HasOne(d => d.Finding).WithMany(p => p.FindingHistories)
                .HasForeignKey(d => d.FindingId)
                .HasConstraintName("FK_FindingHistory_Findings");
        });

        modelBuilder.Entity<InventoryUpload>(entity =>
        {
            entity.ToTable("InventoryUploads", "inventory");

            entity.HasIndex(e => new { e.AssetId, e.Sha256 }, "IX_InventoryUploads_AssetId_Sha256");

            entity.Property(e => e.OriginalFileName).HasMaxLength(255);
            entity.Property(e => e.Sha256)
                .HasMaxLength(32)
                .IsFixedLength();
            entity.Property(e => e.UploadedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysdatetimeoffset())", "DF_InventoryUploads_UploadedAt");
            entity.Property(e => e.UploadedBy).HasMaxLength(450);

            entity.HasOne(d => d.Asset).WithMany(p => p.InventoryUploads)
                .HasForeignKey(d => d.AssetId)
                .HasConstraintName("FK_InventoryUploads_Assets");
        });

        modelBuilder.Entity<NotificationChannel>(entity =>
        {
            entity.ToTable("NotificationChannels", "notify");

            entity.HasIndex(e => e.AssetGroupId, "IX_NotificationChannels_AssetGroupId");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysdatetimeoffset())", "DF_NotificationChannels_CreatedAt");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true, "DF_NotificationChannels_IsEnabled");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Target).HasMaxLength(500);

            entity.HasOne(d => d.AssetGroup).WithMany(p => p.NotificationChannels)
                .HasForeignKey(d => d.AssetGroupId)
                .HasConstraintName("FK_NotificationChannels_AssetGroups");
        });

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.ToTable("NotificationLog", "notify");

            entity.HasIndex(e => new { e.ChannelId, e.CreatedAt }, "IX_NotificationLog_ChannelId_CreatedAt");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysdatetimeoffset())", "DF_NotificationLog_CreatedAt");
            entity.Property(e => e.Error).HasMaxLength(500);

            entity.HasOne(d => d.Channel).WithMany(p => p.NotificationLogs)
                .HasForeignKey(d => d.ChannelId)
                .HasConstraintName("FK_NotificationLog_NotificationChannels");
        });

        modelBuilder.Entity<NotificationRule>(entity =>
        {
            entity.ToTable("NotificationRules", "notify");

            entity.HasIndex(e => e.ChannelId, "IX_NotificationRules_ChannelId");

            entity.Property(e => e.IsEnabled).HasDefaultValue(true, "DF_NotificationRules_IsEnabled");

            entity.HasOne(d => d.Channel).WithMany(p => p.NotificationRules)
                .HasForeignKey(d => d.ChannelId)
                .HasConstraintName("FK_NotificationRules_NotificationChannels");
        });

        modelBuilder.Entity<Vulnerability>(entity =>
        {
            entity.ToTable("Vulnerabilities", "vuln");

            entity.HasIndex(e => e.IsKev, "IX_Vulnerabilities_IsKev").HasFilter("([IsKev]=(1))");

            entity.HasIndex(e => e.ExternalId, "UQ_Vulnerabilities_ExternalId").IsUnique();

            entity.Property(e => e.CvssScore).HasColumnType("decimal(3, 1)");
            entity.Property(e => e.CvssVector)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.EpssPercentile).HasColumnType("decimal(6, 5)");
            entity.Property(e => e.EpssScore).HasColumnType("decimal(6, 5)");
            entity.Property(e => e.ExternalId)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.ModifiedAt).HasPrecision(3);
            entity.Property(e => e.PublishedAt).HasPrecision(3);
            entity.Property(e => e.Source)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Summary).HasMaxLength(500);
            entity.Property(e => e.SyncedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysdatetimeoffset())", "DF_Vulnerabilities_SyncedAt");
            entity.Property(e => e.WithdrawnAt).HasPrecision(3);
        });

        modelBuilder.Entity<VulnerabilityAlias>(entity =>
        {
            entity.HasKey(e => new { e.VulnerabilityId, e.Alias });

            entity.ToTable("VulnerabilityAliases", "vuln");

            entity.HasIndex(e => e.Alias, "IX_VulnerabilityAliases_Alias");

            entity.Property(e => e.Alias)
                .HasMaxLength(64)
                .IsUnicode(false);

            entity.HasOne(d => d.Vulnerability).WithMany(p => p.VulnerabilityAliases)
                .HasForeignKey(d => d.VulnerabilityId)
                .HasConstraintName("FK_VulnerabilityAliases_Vulnerabilities");
        });

        modelBuilder.Entity<VulnerabilityReference>(entity =>
        {
            entity.ToTable("VulnerabilityReferences", "vuln");

            entity.HasIndex(e => e.VulnerabilityId, "IX_VulnerabilityReferences_VulnerabilityId");

            entity.Property(e => e.Type)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Url).HasMaxLength(2000);

            entity.HasOne(d => d.Vulnerability).WithMany(p => p.VulnerabilityReferences)
                .HasForeignKey(d => d.VulnerabilityId)
                .HasConstraintName("FK_VulnerabilityReferences_Vulnerabilities");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
