using System.ComponentModel.DataAnnotations;

namespace ZafiyetRadari.Migrator;

/// <summary>
/// Web ve Worker'ın veritabanı login şifreleri. Asla appsettings'e yazılmaz:
/// geliştirmede User Secrets, production'da ortam değişkeni / Docker secret.
/// </summary>
internal sealed class DatabaseLoginsOptions
{
    public const string SectionName = "DatabaseLogins";

    [Required, MinLength(16), MaxLength(128)]
    public string WebPassword { get; init; } = string.Empty;

    [Required, MinLength(16), MaxLength(128)]
    public string WorkerPassword { get; init; } = string.Empty;
}
