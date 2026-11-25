using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using ZafiyetRadari.Application.Abstractions.Auditing;
using ZafiyetRadari.Application.Abstractions.Identity;

namespace ZafiyetRadari.Web.Security;

/// <summary>
/// Policy kontrolünden 403 alan her isteği audit log'a yazar (yetkisiz erişim denemesi).
/// Sonucu değiştirmez; varsayılan davranışı aynen uygular.
/// </summary>
internal sealed class AuditingAuthorizationResultHandler(ILogger<AuditingAuthorizationResultHandler> logger)
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            await TryAuditAsync(context);
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    private async Task TryAuditAsync(HttpContext context)
    {
        try
        {
            var currentUser = context.RequestServices.GetRequiredService<ICurrentUser>();
            var auditTrail = context.RequestServices.GetRequiredService<IAuditTrail>();

            await auditTrail.AppendAsync(
                new AuditEvent(
                    AuditEventTypes.AccessDenied,
                    currentUser.UserId,
                    currentUser.UserName,
                    currentUser.IpAddress,
                    TargetType: "Path",
                    Details: $"{context.Request.Method} {context.Request.Path}"),
                context.RequestAborted);
        }
        catch (Exception ex)
        {
            // Audit yazılamasa bile istek 403 ile sonuçlanmalı; hata loglanır.
            logger.LogError(ex, "Yetkisiz erişim denemesi audit log'a yazılamadı.");
        }
    }
}
