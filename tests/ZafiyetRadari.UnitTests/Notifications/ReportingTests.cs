using ZafiyetRadari.Application.Dashboard;
using ZafiyetRadari.Web.Reporting;

namespace ZafiyetRadari.UnitTests.Notifications;

public sealed class ReportingTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\")", "\"'=HYPERLINK(\"\"http://evil\"\")\"")]
    [InlineData("+1+1", "'+1+1")]
    [InlineData("-2", "'-2")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("lodash", "lodash")]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("satır\nsonu", "\"satır\nsonu\"")]
    [InlineData(null, "")]
    public void Csv_EscapesFormulasAndSeparators(string? input, string expected)
    {
        Assert.Equal(expected, CsvBuilder.Escape(input));
    }

    [Theory]
    [InlineData("4.1.1", "=\"4.1.1\"")]               // Excel tarihe çevirmesin
    [InlineData("16.2.4-canary.3", "=\"16.2.4-canary.3\"")]
    [InlineData("1\"+HYPERLINK(\"x", "\"1\"\"+HYPERLINK(\"\"x\"")]  // güvenli küme dışı: formüle dönüşmez, kaçışlanır
    public void Csv_TextCells_StayText_WithoutFormulaInjection(string input, string expected)
    {
        Assert.Equal(expected, CsvBuilder.AsText(input));
    }

    [Fact]
    public void Csv_Decimals_UseTurkishFormat()
    {
        var text = System.Text.Encoding.UTF8.GetString(new CsvBuilder().AddRow(87.37m, new CsvText("4.17.20")).ToBytes()[3..]);

        Assert.Equal("87,37;=\"4.17.20\"\r\n", text);
    }

    [Fact]
    public void Csv_StartsWithUtf8Bom()
    {
        var bytes = new CsvBuilder().AddRow("Öncelik").ToBytes();

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
    }

    [Fact]
    public void Trend_FillsMissingDaysWithZero()
    {
        var start = new DateOnly(2026, 11, 1);
        var points = DashboardService.FillGaps([new TrendPoint(new DateOnly(2026, 11, 3), 5, 1)], start, new DateOnly(2026, 11, 4));

        Assert.Equal(4, points.Count);
        Assert.Equal(new TrendPoint(new DateOnly(2026, 11, 1), 0, 0), points[0]);
        Assert.Equal(5, points[2].Opened);
    }
}
