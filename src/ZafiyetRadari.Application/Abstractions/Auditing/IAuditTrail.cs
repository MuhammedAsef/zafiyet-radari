namespace ZafiyetRadari.Application.Abstractions.Auditing;

/// <summary>
/// Hash zincirli, değiştirilemez güvenlik denetim kaydı.
/// Kayıtlar sadece eklenir; uygulamanın güncelleme veya silme yetkisi yoktur.
/// </summary>
public interface IAuditTrail
{
    Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);

    Task<AuditChainStatus> VerifyAsync(CancellationToken cancellationToken = default);
}

/// <summary>Şifre, token, secret gibi değerler hiçbir alana yazılmaz.</summary>
public sealed record AuditEvent(
    string EventType,
    string? UserId = null,
    string? UserName = null,
    string? IpAddress = null,
    string? TargetType = null,
    string? TargetId = null,
    string? Details = null);

/// <param name="IsIntact">Zincirde değiştirilmiş veya aradan silinmiş kayıt yoksa true.</param>
/// <param name="FirstBrokenId">Bozulmanın başladığı ilk kayıt.</param>
/// <param name="HeadId">Zincirin son kaydı. Dışarıya (loglara) yazılarak sondan silmeye karşı çapa görevi görür.</param>
/// <param name="HeadHash">Son kaydın SHA-256 hash'i (hex).</param>
public sealed record AuditChainStatus(bool IsIntact, long? FirstBrokenId, long? HeadId, string? HeadHash);
