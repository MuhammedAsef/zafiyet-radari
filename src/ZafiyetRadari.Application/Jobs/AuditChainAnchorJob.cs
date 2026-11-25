using Microsoft.Extensions.Logging;
using ZafiyetRadari.Application.Abstractions.Auditing;

namespace ZafiyetRadari.Application.Jobs;

/// <summary>
/// Audit zincirini düzenli doğrular ve son kaydın hash'ini loga yazar.
/// Zincir, aradan değiştirilen/silinen kaydı kendisi yakalar; ama sondan silinen kaydı yakalayamaz.
/// Loglara düşen son hash bir "çapa"dır: sonraki çalıştırmada HeadId geriye gittiyse sondan silme olmuştur.
/// </summary>
public sealed class AuditChainAnchorJob(IAuditTrail auditTrail, ILogger<AuditChainAnchorJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var status = await auditTrail.VerifyAsync(cancellationToken);

        if (!status.IsIntact)
        {
            logger.LogCritical(
                "Audit zinciri BOZUK. İlk bozuk kayıt: {FirstBrokenId}. Son kayıt: {HeadId}",
                status.FirstBrokenId,
                status.HeadId);
            return;
        }

        logger.LogInformation(
            "Audit zinciri sağlam. Son kayıt: {HeadId}, hash: {HeadHash}",
            status.HeadId,
            status.HeadHash);
    }
}
