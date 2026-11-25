using Hangfire;

namespace ZafiyetRadari.Web.Ops;

/// <summary>
/// Hangfire panelini (/ops/jobs) uygulamanın koyu temasına uyarlar.
/// CSS ve JS bu derlemeye gömülü kaynaktır; Hangfire bunları kendi birleşik
/// stil/script dosyasının sonuna ekler, böylece ayrı bir statik dosya yolu açılmaz.
/// </summary>
internal static class HangfireDashboardTheme
{
    private const string StylesheetResource = "ZafiyetRadari.Web.Ops.hangfire-theme.css";
    private const string ScriptResource = "ZafiyetRadari.Web.Ops.hangfire-theme.js";

    private static int _applied;

    public static void Apply()
    {
        // Hangfire kaynak listesi statiktir; iki kez eklenirse stil iki kez yüklenir.
        if (Interlocked.Exchange(ref _applied, 1) == 1)
        {
            return;
        }

        var assembly = typeof(HangfireDashboardTheme).Assembly;

        GlobalConfiguration.Configuration
            .UseDashboardStylesheet(assembly, StylesheetResource)
            .UseDashboardJavaScript(assembly, ScriptResource);
    }
}
