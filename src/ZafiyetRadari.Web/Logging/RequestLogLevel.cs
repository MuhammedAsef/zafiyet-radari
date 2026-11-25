using Serilog.Events;

namespace ZafiyetRadari.Web.Logging;

/// <summary>İstek loglarının seviyesini sonuca göre belirler. Güvenlik açısından anlamlı yanıtlar Warning'e çıkar.</summary>
internal static class RequestLogLevel
{
    public static LogEventLevel Get(HttpContext context, double elapsedMilliseconds, Exception? exception)
    {
        if (exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogEventLevel.Error;
        }

        if (context.Response.StatusCode is StatusCodes.Status401Unauthorized
            or StatusCodes.Status403Forbidden
            or StatusCodes.Status429TooManyRequests)
        {
            return LogEventLevel.Warning;
        }

        // Sağlık kontrolü ve Hangfire panelinin 2 saniyelik istatistik yoklaması logları boğmasın.
        return context.Request.Path.StartsWithSegments("/health")
            || context.Request.Path.StartsWithSegments("/ops/jobs/stats")
            ? LogEventLevel.Verbose
            : LogEventLevel.Information;
    }
}
