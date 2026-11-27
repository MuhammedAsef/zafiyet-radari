using System.Globalization;
using System.Text;
using ZafiyetRadari.Application.Abstractions.Notifications;
using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Application.Notifications;

/// <summary>Bulgu bildirim mailleri. Tüm dış içerik (özet, paket adı, grup adı) EmailTemplates ile encode edilir.</summary>
public static class FindingEmails
{
    /// <summary>KEV ekleme tarihi bu kadar yeniyse yükselme sebebi "KEV'e eklendi" olarak yazılır.</summary>
    private static readonly TimeSpan RecentKevWindow = TimeSpan.FromDays(14);

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static EmailMessage NewFindings(
        string to,
        string groupName,
        IReadOnlyList<FindingEmailItem> items,
        int totalCount,
        IAppLinks links,
        int assetGroupId)
    {
        var urgent = items.Count(i => i.Priority == FindingPriority.Now);
        var subject = urgent > 0
            ? $"[Zafiyet Radarı] {groupName}: {urgent} acil, toplam {totalCount} yeni bulgu"
            : $"[Zafiyet Radarı] {groupName}: {totalCount} yeni bulgu";

        var intro = $"{groupName} grubunda {totalCount.ToString(CultureInfo.InvariantCulture)} yeni veya yeniden açılan bulgu var.";

        return FindingListMail(to, subject, "Yeni bulgular", intro, items, totalCount, links, assetGroupId, Label);
    }

    /// <summary>Önceliği yükselen açık bulgular. Sebep satır bazında yazılır: yakın zamanda KEV'e eklenme veya risk artışı.</summary>
    public static EmailMessage Escalated(
        string to,
        string groupName,
        IReadOnlyList<FindingEmailItem> items,
        int totalCount,
        IAppLinks links,
        int assetGroupId,
        DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var newlyKev = items.Count(i => IsRecentlyKev(i, today));

        var subject = newlyKev > 0
            ? $"[Zafiyet Radarı] {groupName}: {newlyKev} bulgu CISA KEV listesine girdi"
            : $"[Zafiyet Radarı] {groupName}: {totalCount} bulgunun önceliği yükseldi";

        var intro = $"{groupName} grubunda daha önce bildirilen {totalCount.ToString(CultureInfo.InvariantCulture)} açık bulgu artık daha acil. "
            + "CISA KEV listesi, saldırganların bu zafiyeti şu an gerçekten kullandığını gösterir.";

        return FindingListMail(
            to, subject, "Önceliği yükselen bulgular", intro, items, totalCount, links, assetGroupId,
            item => IsRecentlyKev(item, today)
                ? $"{PriorityLabel(item.Priority)} · KEV'e eklendi {item.KevAddedAt!.Value.ToString("dd.MM.yyyy", Turkish)}"
                : $"{Label(item)} · risk arttı");
    }

    public static EmailMessage WeeklyDigest(string to, string groupName, GroupDigest digest, IAppLinks links, int assetGroupId)
    {
        var now = digest.Count(FindingPriority.Now);
        var subject = now > 0
            ? $"[Zafiyet Radarı] {groupName} haftalık özet: {digest.OpenTotal} açık bulgu, {now} acil"
            : $"[Zafiyet Radarı] {groupName} haftalık özet: {digest.OpenTotal} açık bulgu";

        var html = new StringBuilder();
        html.Append("<p><strong>").Append(EmailTemplates.Encode(groupName)).Append("</strong> grubunun son 7 günü.</p>");

        // Önceliğe göre açık bulgular: dört hücreli tek satır. Renkler arayüzdeki öncelik renkleriyle aynı.
        html.Append("<table style=\"width:100%;border-collapse:separate;border-spacing:6px 0;margin:8px 0 16px\"><tr>");
        AppendStat(html, "Hemen", digest.Count(FindingPriority.Now), "#d6334a");
        AppendStat(html, "Bu hafta", digest.Count(FindingPriority.ThisWeek), "#c98a12");
        AppendStat(html, "Planla", digest.Count(FindingPriority.Plan), "#2f7fd8");
        AppendStat(html, "İzle", digest.Count(FindingPriority.Watch), "#6c757d");
        html.Append("</tr></table>");

        html.Append("<p style=\"margin:0 0 16px\">Bu hafta <strong>").Append(digest.NewThisPeriod.ToString(CultureInfo.InvariantCulture))
            .Append("</strong> yeni bulgu çıktı, <strong>").Append(digest.FixedThisPeriod.ToString(CultureInfo.InvariantCulture))
            .Append("</strong> bulgu düzeltildi.");

        if (digest.OpenKev > 0)
        {
            html.Append(" Açık bulgulardan <strong style=\"color:#d6334a\">").Append(digest.OpenKev.ToString(CultureInfo.InvariantCulture))
                .Append("</strong> tanesi CISA KEV listesinde, yani aktif olarak istismar ediliyor.");
        }

        html.Append("</p>");

        var text = new StringBuilder();
        text.Append(groupName).AppendLine(" grubunun son 7 günü.").AppendLine()
            .Append("Açık bulgular: Hemen ").Append(digest.Count(FindingPriority.Now))
            .Append(", Bu hafta ").Append(digest.Count(FindingPriority.ThisWeek))
            .Append(", Planla ").Append(digest.Count(FindingPriority.Plan))
            .Append(", İzle ").Append(digest.Count(FindingPriority.Watch)).AppendLine()
            .Append("Bu hafta ").Append(digest.NewThisPeriod).Append(" yeni bulgu, ").Append(digest.FixedThisPeriod).AppendLine(" düzeltildi.");

        if (digest.OpenKev > 0)
        {
            text.Append("CISA KEV listesindeki açık bulgu: ").Append(digest.OpenKev).AppendLine();
        }

        if (digest.TopFindings.Count > 0)
        {
            html.Append("<p style=\"margin:16px 0 6px;font-weight:600\">En acil açık bulgular</p>");
            text.AppendLine().AppendLine("En acil açık bulgular:");
            AppendFindingTable(html, text, digest.TopFindings, links, Label);
        }
        else
        {
            html.Append("<p>Açık bulgu kalmadı.</p>");
            text.AppendLine("Açık bulgu kalmadı.");
        }

        AppendFooterLinks(html, text, links, assetGroupId);

        return new EmailMessage(to, EmailTemplates.Subject(subject), EmailTemplates.Layout("Haftalık özet", html.ToString()), text.ToString());
    }

    public static EmailMessage Test(string to, string groupName, string channelName, IAppLinks links, int assetGroupId) =>
        new(
            to,
            EmailTemplates.Subject($"[Zafiyet Radarı] Test bildirimi: {channelName}"),
            EmailTemplates.Layout(
                "Test bildirimi",
                $"<p><strong>{EmailTemplates.Encode(groupName)}</strong> grubundaki <strong>{EmailTemplates.Encode(channelName)}</strong> kanalı çalışıyor.</p>"
                + EmailTemplates.Button(links.AssetGroupDetails(assetGroupId), "Gruba git")),
            $"{groupName} grubundaki {channelName} kanalı çalışıyor.\n{links.AssetGroupDetails(assetGroupId)}");

    private static EmailMessage FindingListMail(
        string to,
        string subject,
        string title,
        string intro,
        IReadOnlyList<FindingEmailItem> items,
        int totalCount,
        IAppLinks links,
        int assetGroupId,
        Func<FindingEmailItem, string> badge)
    {
        var html = new StringBuilder();
        html.Append("<p>").Append(EmailTemplates.Encode(intro)).Append("</p>");

        var text = new StringBuilder();
        text.AppendLine(intro).AppendLine();

        AppendFindingTable(html, text, items, links, badge);

        if (totalCount > items.Count)
        {
            html.Append("<p>… ve ").Append((totalCount - items.Count).ToString(CultureInfo.InvariantCulture)).Append(" bulgu daha.</p>");
            text.Append("... ve ").Append(totalCount - items.Count).AppendLine(" bulgu daha.");
        }

        AppendFooterLinks(html, text, links, assetGroupId);

        return new EmailMessage(to, EmailTemplates.Subject(subject), EmailTemplates.Layout(title, html.ToString()), text.ToString());
    }

    private static void AppendFindingTable(
        StringBuilder html,
        StringBuilder text,
        IReadOnlyList<FindingEmailItem> items,
        IAppLinks links,
        Func<FindingEmailItem, string> badge)
    {
        html.Append("<table style=\"width:100%;border-collapse:collapse;font-size:14px\">")
            .Append("<tr style=\"text-align:left;border-bottom:1px solid #dee2e6\"><th style=\"padding:6px\">Öncelik</th><th style=\"padding:6px\">Zafiyet</th><th style=\"padding:6px\">Paket</th><th style=\"padding:6px\">Varlık</th></tr>");

        foreach (var item in items)
        {
            var label = badge(item);
            html.Append("<tr style=\"border-bottom:1px solid #f1f3f5\">")
                .Append("<td style=\"padding:6px;white-space:nowrap\">").Append(EmailTemplates.Encode(label)).Append("</td>")
                .Append("<td style=\"padding:6px\">").Append(EmailTemplates.Link(links.FindingDetails(item.FindingId), item.Title));

            if (!string.IsNullOrWhiteSpace(item.Summary))
            {
                html.Append("<div style=\"color:#6c757d;font-size:12px\">").Append(EmailTemplates.Encode(Truncate(item.Summary, 140))).Append("</div>");
            }

            html.Append("</td>")
                .Append("<td style=\"padding:6px\"><code>").Append(EmailTemplates.Encode($"{item.PackageName}@{item.Version}")).Append("</code></td>")
                .Append("<td style=\"padding:6px\">").Append(EmailTemplates.Encode(item.AssetName)).Append("</td>")
                .Append("</tr>");

            text.Append("- [").Append(label).Append("] ").Append(item.Title).Append(" — ")
                .Append(item.PackageName).Append('@').Append(item.Version).Append(" (").Append(item.AssetName).Append(")\n  ")
                .AppendLine(links.FindingDetails(item.FindingId));
        }

        html.Append("</table>");
    }

    private static void AppendFooterLinks(StringBuilder html, StringBuilder text, IAppLinks links, int assetGroupId)
    {
        var findingsUrl = $"{links.BaseUrl}/Findings/Findings?groupId={assetGroupId.ToString(CultureInfo.InvariantCulture)}";
        html.Append(EmailTemplates.Button(findingsUrl, "Tüm bulguları gör"));
        text.AppendLine().Append("Tüm bulgular: ").AppendLine(findingsUrl)
            .Append("Bildirim ayarları: ").AppendLine(links.AssetGroupDetails(assetGroupId));
    }

    private static void AppendStat(StringBuilder html, string label, int count, string color) =>
        html.Append("<td style=\"width:25%;padding:10px 12px;background:#f8f9fa;border-radius:6px;border-top:3px solid ").Append(color).Append("\">")
            .Append("<div style=\"font-size:22px;font-weight:600;color:#212529\">").Append(count.ToString(CultureInfo.InvariantCulture)).Append("</div>")
            .Append("<div style=\"font-size:12px;color:#6c757d\">").Append(EmailTemplates.Encode(label)).Append("</div>")
            .Append("</td>");

    private static bool IsRecentlyKev(FindingEmailItem item, DateOnly today) =>
        item.IsKev && item.KevAddedAt is { } added && added >= today.AddDays(-(int)RecentKevWindow.TotalDays);

    private static string Label(FindingEmailItem item)
    {
        if (item.IsMaliciousPackage)
        {
            return "KÖTÜ AMAÇLI PAKET";
        }

        var priority = PriorityLabel(item.Priority);
        return item.IsKev ? $"{priority} · KEV" : priority;
    }

    private static string PriorityLabel(FindingPriority priority) => priority switch
    {
        FindingPriority.Now => "Hemen",
        FindingPriority.ThisWeek => "Bu hafta",
        FindingPriority.Plan => "Planla",
        _ => "İzle"
    };

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max] + "…";
}
