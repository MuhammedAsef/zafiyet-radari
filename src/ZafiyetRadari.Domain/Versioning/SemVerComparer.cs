using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Domain.Versioning;

/// <summary>npm ve NuGet: ikisi de SemVer tabanlı (NuGet'in 4. bileşeni SemanticVersion'da desteklenir).</summary>
public sealed class SemVerComparer : IVersionComparer
{
    public bool Supports(string ecosystem) => ecosystem is Ecosystems.Npm or Ecosystems.NuGet;

    public RangeEvaluation Evaluate(string version, VersionRangeSpec range)
    {
        ArgumentNullException.ThrowIfNull(range);

        if (!SemanticVersion.TryParse(version, out var candidate))
        {
            return RangeEvaluation.Unknown;
        }

        if (!IsUnbounded(range.Introduced))
        {
            if (!SemanticVersion.TryParse(range.Introduced, out var introduced))
            {
                return RangeEvaluation.Unknown;
            }

            if (candidate < introduced)
            {
                return RangeEvaluation.NotAffected;
            }
        }

        if (!string.IsNullOrWhiteSpace(range.Fixed))
        {
            if (!SemanticVersion.TryParse(range.Fixed, out var fixedVersion))
            {
                return RangeEvaluation.Unknown;
            }

            // Fixed hariç: düzeltmeyi içeren ilk sürüm artık etkilenmez.
            if (candidate >= fixedVersion)
            {
                return RangeEvaluation.NotAffected;
            }
        }

        if (!string.IsNullOrWhiteSpace(range.LastAffected))
        {
            if (!SemanticVersion.TryParse(range.LastAffected, out var lastAffected))
            {
                return RangeEvaluation.Unknown;
            }

            // LastAffected dahil.
            if (candidate > lastAffected)
            {
                return RangeEvaluation.NotAffected;
            }
        }

        return RangeEvaluation.Affected;
    }

    private static bool IsUnbounded(string? introduced) =>
        string.IsNullOrWhiteSpace(introduced) || introduced.Trim() == "0";
}
