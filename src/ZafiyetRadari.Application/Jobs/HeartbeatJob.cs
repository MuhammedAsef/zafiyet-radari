using Microsoft.Extensions.Logging;

namespace ZafiyetRadari.Application.Jobs;

/// <summary>Worker'ın ve Hangfire kuyruğunun çalıştığını doğrulayan örnek iş.</summary>
public sealed class HeartbeatJob(ILogger<HeartbeatJob> logger, TimeProvider timeProvider)
{
    public Task RunAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker ayakta. Zaman: {UtcNow}", timeProvider.GetUtcNow());
        return Task.CompletedTask;
    }
}
