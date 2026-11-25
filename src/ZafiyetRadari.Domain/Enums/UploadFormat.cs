namespace ZafiyetRadari.Domain.Enums;

/// <summary>Yüklenen envanter dosyasının biçimi. Değerler veritabanındaki CHECK constraint'leriyle birebir aynıdır.</summary>
public enum UploadFormat : byte
{
    /// <summary>CycloneDX JSON SBOM.</summary>
    CycloneDx = 1,

    /// <summary>package-lock.json</summary>
    NpmLock = 2,

    /// <summary>packages.lock.json</summary>
    NuGetLock = 3
}
