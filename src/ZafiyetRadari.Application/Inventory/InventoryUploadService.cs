using System.Globalization;
using System.Security.Cryptography;
using FluentValidation;
using Microsoft.Extensions.Logging;
using ZafiyetRadari.Application.Abstractions.Auditing;
using ZafiyetRadari.Application.Abstractions.Jobs;
using ZafiyetRadari.Application.Abstractions.Persistence;
using ZafiyetRadari.Application.Common;
using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;
using ZafiyetRadari.Domain.Inventory;

namespace ZafiyetRadari.Application.Inventory;

public interface IInventoryUploadService
{
    Task<Result<UploadResult>> UploadAsync(UploadInventoryCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Envanter dosyası yükleme. Güvenlik önlemleri:
/// - Dosya diske yazılmaz, bellekte işlenir; okunan bayt sayısı sınırlıdır (Content-Length'e güvenilmez).
/// - Biçim içerikten tanınır, dosya adı sadece gösterim içindir ve temizlenir.
/// - JSON derinliği ve paket sayısı sınırlıdır (ayrıştırıcıda).
/// - Her paket adı/sürümü elle girişle aynı kurallarla doğrulanır; purl bizim tarafımızdan üretilir.
/// - SHA-256 ile aynı dosyanın tekrar yüklenmesi engellenir ve bütünlük kaydı tutulur.
/// </summary>
internal sealed class InventoryUploadService(
    InventoryAccessGuard guard,
    IInventoryRepository repository,
    IInventoryFileParser parser,
    IUnitOfWork unitOfWork,
    IAuditTrail auditTrail,
    TimeProvider timeProvider,
    IValidator<UploadInventoryCommand> validator,
    IMatchingScheduler matchingScheduler,
    ILogger<InventoryUploadService> logger) : IInventoryUploadService
{
    public async Task<Result<UploadResult>> UploadAsync(UploadInventoryCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateToResultAsync(command, cancellationToken);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var groupId = await repository.GetAssetGroupIdAsync(command.AssetId, cancellationToken);
        if (groupId is null)
        {
            return AssetErrors.NotFound;
        }

        var access = await guard.RequireAsync(groupId.Value, AssetGroupRole.Editor, cancellationToken);
        if (access.IsFailure)
        {
            return access.Error == AssetGroupErrors.NotFound ? AssetErrors.NotFound : access.Error;
        }

        var content = await ReadLimitedAsync(command.Content, InventoryLimits.MaxUploadBytes, cancellationToken);
        if (content is null)
        {
            return UploadErrors.TooLarge(InventoryLimits.MaxUploadMegabytes);
        }

        if (content.Length == 0)
        {
            return UploadErrors.Empty;
        }

        var sha256 = SHA256.HashData(content);
        if (await repository.UploadExistsAsync(command.AssetId, sha256, cancellationToken))
        {
            return UploadErrors.AlreadyUploaded;
        }

        var parsed = parser.Parse(content);
        if (parsed.IsFailure)
        {
            return parsed.Error;
        }

        var (coordinates, invalid) = ToCoordinates(parsed.Value.Packages);
        if (coordinates.Count == 0)
        {
            return UploadErrors.NoComponents;
        }

        var asset = await repository.GetAssetWithComponentsAsync(command.AssetId, cancellationToken);
        if (asset is null)
        {
            return AssetErrors.NotFound;
        }

        var now = timeProvider.GetUtcNow();
        var upload = InventoryUpload.Create(command.FileName, parsed.Value.Format, sha256, content.Length, access.Value, now);
        var summary = asset.ApplyUpload(upload, coordinates, parsed.Value.CoveredEcosystems, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Bulgular arka planda üretilir; kullanıcı yükleme sonucunu beklemeden görür.
        matchingScheduler.ScheduleAssetMatching(command.AssetId);

        var skipped = parsed.Value.IgnoredCount + invalid;
        var shaHex = Convert.ToHexString(sha256);
        logger.LogInformation(
            "Envanter yüklendi. Varlık {AssetId}, biçim {Format}, {Added} yeni, {Reactivated} yeniden aktif, {Deactivated} pasif, {Skipped} atlandı, SHA-256 {Sha256}",
            command.AssetId, parsed.Value.Format, summary.Added, summary.Reactivated, summary.Deactivated, skipped, shaHex);

        var user = guard.CurrentUser;
        await auditTrail.AppendAsync(
            new AuditEvent(
                AuditEventTypes.InventoryUploaded, user.UserId, user.UserName, user.IpAddress,
                TargetType: "Asset",
                TargetId: command.AssetId.ToString(CultureInfo.InvariantCulture),
                Details: string.Create(CultureInfo.InvariantCulture,
                    $"Upload {upload.Id}; {parsed.Value.Format}; {content.Length} bayt; SHA-256 {shaHex}; +{summary.Added} ~{summary.Reactivated} -{summary.Deactivated}")),
            cancellationToken);

        return new UploadResult(upload.Id, parsed.Value.Format, summary.Added, summary.Reactivated, summary.Unchanged, summary.Deactivated, skipped);
    }

    /// <summary>En fazla <paramref name="maxBytes"/> okur; sınır aşılırsa null döner (akışın tamamı okunmaz).</summary>
    internal static async Task<byte[]?> ReadLimitedAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;

        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    internal static (List<PackageCoordinate> Coordinates, int Invalid) ToCoordinates(IReadOnlyList<RawPackage> packages)
    {
        var coordinates = new List<PackageCoordinate>(packages.Count);
        var invalid = 0;

        foreach (var package in packages)
        {
            var coordinate = PackageCoordinate.Create(package.Ecosystem, package.Name, package.Version);
            if (coordinate.IsSuccess)
            {
                coordinates.Add(coordinate.Value);
            }
            else
            {
                invalid++;
            }
        }

        return (coordinates, invalid);
    }
}
