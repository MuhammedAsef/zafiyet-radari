using Hangfire;
using Hangfire.Dashboard;
using ZafiyetRadari.Application.Abstractions.Identity;

namespace ZafiyetRadari.Web.Security;

/// <summary>
/// Hangfire paneli sadece Admin'e açık. Endpoint ayrıca RequireAdmin policy'siyle korunur;
/// bu filtre Hangfire'ın kendi kontrolüdür (varsayılan "sadece localhost" filtresinin yerine geçer).
/// </summary>
internal sealed class HangfireDashboardAdminFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) =>
        context.GetHttpContext().User.IsInRole(AppRoles.Admin);
}
