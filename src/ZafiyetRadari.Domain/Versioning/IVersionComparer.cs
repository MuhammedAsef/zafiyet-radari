namespace ZafiyetRadari.Domain.Versioning;

/// <summary>
/// Ekosisteme özel sürüm karşılaştırma stratejisi (Strategy deseni).
/// Yeni bir ekosistem (Maven, PyPI, Debian...) eklemek için mevcut koda dokunmadan yeni bir implementasyon yazılır.
/// </summary>
public interface IVersionComparer
{
    bool Supports(string ecosystem);

    RangeEvaluation Evaluate(string version, VersionRangeSpec range);
}
