using System.Globalization;
using System.Text;
using ZafiyetRadari.Application.Abstractions.Notifications;
using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Application.Notifications;

/// <summary>Bulgu bildirim maili. Tüm dış içerik (özet, paket adı) EmailTemplates ile encode edilir.</summary>
public static class FindingEmails
{
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

        var html = new StringBuilder();
        html.Append("<p><strong>").Append(EmailTemplates.Encode(groupName)).Append("</strong> grubunda ")
            .Append(totalCount.ToString(CultureInfo.InvariantCulture))
            .Append(" yeni veya yeniden açılan bulgu var.</p>")
            .Append("<table style=\"width:100%;border-collapse:collapse;font-size:14px\">")
            .Append("<tr style=\"text-align:left;border-bottom:1px solid #dee2e6\"><th style=\"padding:6px\">Öncelik</th><th style=\"padding:6px\">Zafiyet</th><th style=\"padding:6px\">Paket</th><th style=\"padding:6px\">Varlık</th></tr>");

        var text = new StringBuilder();
        text.Append(groupName).Append(" grubunda ").Append(totalCount).AppendLine(" yeni veya yeniden açılan bulgu var.").AppendLine();

        foreach (var item in items)
        {
            var badge = Label(item);
            html.Append("<tr style=\"border-bottom:1px solid #f1f3f5\">")
                .Append("<td style=\"padding:6px;white-space:nowrap\">").Append(EmailTemplates.Encode(badge)).Append("</td>")
                .Append("<td style=\"padding:6px\">").Append(EmailTemplates.Link(links.FindingDetails(item.FindingId), item.Title));

            if (!string.IsNullOrWhiteSpace(item.Summary))
            {
                html.Append("<div style=\"color:#6c757d;font-size:12px\">").Append(EmailTemplates.Encode(Truncate(item.Summary, 140))).Append("</div>");
            }

            html.Append("</td>")
                .Append("<td style=\"padding:6px\"><code>").Append(EmailTemplates.Encode($"{item.PackageName}@{item.Version}")).Append("</code></td>")
                .Append("<td style=\"padding:6px\">").Append(EmailTemplates.Encode(item.AssetName)).Append("</td>")
                .Append("</tr>");

            text.Append("- [").Append(badge).Append("] ").Append(item.Title).Append(" — ")
                .Append(item.PackageName).Append('@').Append(item.Version).Append(" (").Append(item.AssetName).Append(")\n  ")
                .AppendLine(links.FindingDetails(item.FindingId));
        }

        html.Append("</table>");

        if (totalCount > items.Count)
        {
            html.Append("<p>… ve ").Append((totalCount - items.Count).ToString(CultureInfo.InvariantCulture)).Append(" bulgu daha.</p>");
            text.Append("... ve ").Append(totalCount - items.Count).AppendLine(" bulgu daha.");
        }

        var groupUrl = links.AssetGroupDetails(assetGroupId);
        html.Append(EmailTemplates.Button($"{links.BaseUrl}/Findings/Findings?groupId={assetGroupId}", "Tüm bulguları gör"));
        text.AppendLine().Append("Tüm bulgular: ").Append(links.BaseUrl).Append("/Findings/Findings?groupId=").Append(assetGroupId).AppendLine()
            .Append("Bildirim ayarları: ").AppendLine(groupUrl);

        return new EmailMessage(to, EmailTemplates.Subject(subject), EmailTemplates.Layout("Yeni bulgular", html.ToString()), text.ToString());
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

    private static string Label(FindingEmailItem item)
    {
        if (item.IsMaliciousPackage)
        {
            return "KÖTÜ AMAÇLI PAKET";
        }

        var priority = item.Priority switch
        {
            FindingPriority.Now => "Hemen",
            FindingPriority.ThisWeek => "Bu hafta",
            FindingPriority.Plan => "Planla",
            _ => "İzle"
        };

        return item.IsKev ? $"{priority} · KEV" : priority;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max] + "…";
}
