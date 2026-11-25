using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;
using ZafiyetRadari.Domain.Inventory;

namespace ZafiyetRadari.Domain.Entities;

/// <summary>
/// Varlık (uygulama, servis, sunucu) ve bileşen envanteri. Bileşenler sadece bu metotlarla değişir.
/// Bileşenler silinmez, pasife alınır: bulgu geçmişi korunur ve aynı sürüm geri gelirse kayıt yeniden etkinleşir.
/// </summary>
public partial class Asset
{
    public const int MaxNameLength = 200;

    public AssetEnvironment EnvironmentKind => (AssetEnvironment)Environment;

    public AssetCriticality CriticalityLevel => (AssetCriticality)Criticality;

    public static Result<Asset> Create(
        int assetGroupId,
        string name,
        AssetEnvironment environment,
        AssetCriticality criticality,
        bool isInternetFacing,
        DateTimeOffset now)
    {
        var validation = Validate(ref name, environment, criticality);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        return new Asset
        {
            AssetGroupId = assetGroupId,
            Name = name,
            Environment = (byte)environment,
            Criticality = (byte)criticality,
            IsInternetFacing = isInternetFacing,
            CreatedAt = now
        };
    }

    public Result Update(
        string name,
        AssetEnvironment environment,
        AssetCriticality criticality,
        bool isInternetFacing,
        DateTimeOffset now)
    {
        var validation = Validate(ref name, environment, criticality);
        if (validation.IsFailure)
        {
            return validation;
        }

        Name = name;
        Environment = (byte)environment;
        Criticality = (byte)criticality;
        IsInternetFacing = isInternetFacing;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Elle bileşen ekler. Aynı paket sürümü pasifse yeniden etkinleştirir.</summary>
    public Result<Component> AddManualComponent(PackageCoordinate coordinate, DateTimeOffset now)
    {
        var existing = FindComponent(coordinate);
        if (existing is not null)
        {
            if (existing.IsActive)
            {
                return ComponentErrors.AlreadyExists;
            }

            existing.IsActive = true;
            existing.Source = (byte)ComponentSource.Manual;
            existing.Upload = null;
            existing.UploadId = null;
            UpdatedAt = now;
            return existing;
        }

        var component = Component.Create(coordinate, ComponentSource.Manual, upload: null, now);
        Components.Add(component);
        UpdatedAt = now;
        return component;
    }

    public Result DeactivateComponent(long componentId, DateTimeOffset now)
    {
        var component = Components.FirstOrDefault(c => c.Id == componentId && c.IsActive);
        if (component is null)
        {
            return Result.Failure(ComponentErrors.NotFound);
        }

        component.IsActive = false;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>
    /// Yüklenen dosyanın içeriğini envantere uygular.
    /// Kapsam: sadece dosyanın kapsadığı ekosistemlerdeki "yüklemeden gelmiş" bileşenler pasife alınır.
    /// Böylece aynı varlığa ayrı ayrı yüklenen npm ve NuGet lock dosyaları birbirini silmez,
    /// elle eklenen bileşenlere de dokunulmaz.
    /// </summary>
    public InventorySyncSummary ApplyUpload(
        InventoryUpload upload,
        IReadOnlyCollection<PackageCoordinate> coordinates,
        IReadOnlyCollection<string> coveredEcosystems,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(upload);

        int added = 0, reactivated = 0, unchanged = 0, deactivated = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = BuildComponentIndex();

        foreach (var coordinate in coordinates)
        {
            if (!seen.Add(coordinate.Key))
            {
                continue; // dosyada tekrar eden paket
            }

            if (!index.TryGetValue(coordinate.Key, out var component))
            {
                component = Component.Create(coordinate, ComponentSource.Upload, upload, now);
                Components.Add(component);
                index[coordinate.Key] = component;
                added++;
                continue;
            }

            if (component.IsActive)
            {
                if (component.SourceKind == ComponentSource.Upload)
                {
                    component.Upload = upload; // en son hangi dosyada görüldüğü
                }

                unchanged++;
            }
            else
            {
                component.IsActive = true;
                component.Source = (byte)ComponentSource.Upload;
                component.Upload = upload;
                reactivated++;
            }
        }

        foreach (var component in Components)
        {
            if (component.IsActive
                && component.SourceKind == ComponentSource.Upload
                && coveredEcosystems.Contains(component.Ecosystem, StringComparer.Ordinal)
                && !seen.Contains(component.Coordinate.Key))
            {
                component.IsActive = false;
                deactivated++;
            }
        }

        upload.ComponentCount = seen.Count;
        InventoryUploads.Add(upload);
        UpdatedAt = now;

        return new InventorySyncSummary(added, reactivated, unchanged, deactivated);
    }

    private Component? FindComponent(PackageCoordinate coordinate) =>
        Components.FirstOrDefault(c => c.Coordinate.Key == coordinate.Key);

    // Veritabanı tekil indeksi büyük/küçük harf duyarsız olduğundan aynı anahtarda iki kayıt olamaz; yine de ilki alınır.
    private Dictionary<string, Component> BuildComponentIndex()
    {
        var index = new Dictionary<string, Component>(StringComparer.Ordinal);
        foreach (var component in Components)
        {
            index.TryAdd(component.Coordinate.Key, component);
        }

        return index;
    }

    private static Result Validate(ref string name, AssetEnvironment environment, AssetCriticality criticality)
    {
        name = name?.Trim() ?? string.Empty;

        if (name.Length == 0)
        {
            return Result.Failure(AssetErrors.NameRequired);
        }

        if (name.Length > MaxNameLength)
        {
            return Result.Failure(AssetErrors.NameTooLong);
        }

        if (!Enum.IsDefined(environment) || !Enum.IsDefined(criticality))
        {
            return Result.Failure(AssetErrors.InvalidClassification);
        }

        return Result.Success();
    }
}
