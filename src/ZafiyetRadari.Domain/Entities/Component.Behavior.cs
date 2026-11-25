using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Inventory;

namespace ZafiyetRadari.Domain.Entities;

public partial class Component
{
    public ComponentSource SourceKind => (ComponentSource)Source;

    /// <summary>
    /// Kayıtlı alanlardan kimlik. Veritabanındaki değerler zaten doğrulanarak yazıldığı için tekrar doğrulanmaz;
    /// sadece tekilleştirme anahtarı ve purl için kullanılır.
    /// </summary>
    public PackageCoordinate Coordinate => PackageCoordinate.FromTrusted(Ecosystem, Name, Version);

    internal static Component Create(PackageCoordinate coordinate, ComponentSource source, InventoryUpload? upload, DateTimeOffset now) =>
        new()
        {
            Ecosystem = coordinate.Ecosystem,
            Name = coordinate.Name,
            Version = coordinate.Version,
            Purl = coordinate.Purl,
            Source = (byte)source,
            Upload = upload,
            IsActive = true,
            CreatedAt = now
        };
}
