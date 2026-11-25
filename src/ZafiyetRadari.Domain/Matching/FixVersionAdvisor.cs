using ZafiyetRadari.Domain.Versioning;

namespace ZafiyetRadari.Domain.Matching;

/// <summary>
/// "Hangi sürüme geçmeliyim?" sorusunun cevabı: mevcut sürümden büyük en küçük düzeltilmiş sürüm.
/// En az değişiklikle (çoğu zaman yama sürümü) güvenli hale gelmek hedeflenir. Ön sürümler (beta, rc) önerilmez,
/// sadece başka seçenek yoksa kullanılır.
/// </summary>
public static class FixVersionAdvisor
{
    public static string? Recommend(string currentVersion, IEnumerable<string?> fixedVersions)
    {
        ArgumentNullException.ThrowIfNull(fixedVersions);

        if (!SemanticVersion.TryParse(currentVersion, out var current))
        {
            return null;
        }

        var candidates = fixedVersions
            .Select(text => SemanticVersion.TryParse(text, out var parsed) ? (Text: text!, Version: parsed) : default)
            .Where(c => c.Version is not null && c.Version > current)
            .ToList();

        var stable = candidates.Where(c => !c.Version!.IsPreRelease).ToList();
        var pool = stable.Count > 0 ? stable : candidates;

        return pool.Count == 0 ? null : pool.MinBy(c => c.Version)!.Text;
    }
}
