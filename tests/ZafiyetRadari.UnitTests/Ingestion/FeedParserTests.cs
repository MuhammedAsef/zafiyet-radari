using ZafiyetRadari.Infrastructure.Ingestion;
using ZafiyetRadari.Infrastructure.Ingestion.Epss;
using ZafiyetRadari.Infrastructure.Ingestion.Kev;
using ZafiyetRadari.Infrastructure.Ingestion.Osv;

namespace ZafiyetRadari.UnitTests.Ingestion;

public sealed class FeedParserTests
{
    [Fact]
    public void Epss_ParsesScoreDateAndRows_SkippingInvalidLines()
    {
        const string csv = """
            #model_version:v2025.03.14,score_date:2026-11-21T12:55:00Z
            cve,epss,percentile
            CVE-2021-44228,0.944,0.99999
            CVE-2024-0001,0.000123456,0.1
            CVE-2024-0002,1.5,0.5
            not-a-cve,0.1,0.1
            CVE-2024-0003,abc,0.1
            CVE-2021-44228,0.97,0.99999
            """;

        var dataset = EpssCsvParser.Parse(new StringReader(csv));

        Assert.Equal(new DateOnly(2026, 11, 21), dataset.ScoreDate);
        Assert.Equal(2, dataset.Rows.Count);
        Assert.Contains(dataset.Rows, r => r.Cve == "CVE-2021-44228" && r.Score == 0.97m); // tekrar edende sonuncusu
        Assert.Contains(dataset.Rows, r => r.Cve == "CVE-2024-0001" && r.Score == 0.00012m); // 5 basamağa yuvarlanır
    }

    [Theory]
    [InlineData("CVE-2021-44228", true)]
    [InlineData("CVE-2024-1234567", true)]
    [InlineData("cve-2021-44228", false)]
    [InlineData("CVE-2021-4422'; DROP", false)]
    [InlineData("GHSA-35jh-r3h4-6jhm", false)]
    public void CveId_Validation(string value, bool expected)
    {
        Assert.Equal(expected, CveId.IsValid(value));
    }

    [Fact]
    public void Kev_MapsEntries_AndFlagsRansomware()
    {
        var catalog = new KevCatalog
        {
            CatalogVersion = "2026.11.20",
            Vulnerabilities =
            [
                new KevEntry { CveId = "CVE-2021-44228", DateAdded = "2021-12-10", DueDate = "2021-12-24", KnownRansomwareCampaignUse = "Known" },
                new KevEntry { CveId = "CVE-2023-0001", DateAdded = "2023-01-01", KnownRansomwareCampaignUse = "Unknown" },
                new KevEntry { CveId = "bad", DateAdded = "2023-01-01" },
                new KevEntry { CveId = "CVE-2023-0002", DateAdded = "01/01/2023" }
            ]
        };

        var rows = KevFeed.Map(catalog);

        Assert.Equal(2, rows.Count);
        Assert.Contains(new KevRow("CVE-2021-44228", new DateOnly(2021, 12, 10), new DateOnly(2021, 12, 24), true), rows);
        Assert.Contains(new KevRow("CVE-2023-0001", new DateOnly(2023, 1, 1), null, false), rows);
    }

    [Theory]
    [InlineData("2026-11-21T10:00:00Z,GHSA-xxxx-yyyy-zzzz", "GHSA-xxxx-yyyy-zzzz")]
    [InlineData("2026-11-21T10:00:00Z,npm/MAL-2026-1", "MAL-2026-1")]
    public void ModifiedIdCsv_ParsesLines(string line, string expectedId)
    {
        Assert.True(ModifiedIdCsv.TryParseLine(line, out var modified, out var id));
        Assert.Equal(expectedId, id);
        Assert.Equal(new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero), modified);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-comma")]
    [InlineData("not-a-date,GHSA-1")]
    public void ModifiedIdCsv_RejectsBadLines(string line)
    {
        Assert.False(ModifiedIdCsv.TryParseLine(line, out _, out _));
    }
}
