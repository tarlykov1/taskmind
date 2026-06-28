using System.IO.Compression;
using GSPTaskMiningAnalyzer;
using GSPTaskMiningAnalyzer.Models;
using Xunit;

namespace GSPTaskMiningAnalyzer.Tests;

public sealed class AnalyzerTests
{
    [Fact]
    public void DurationLimitsLargeGaps()
    {
        var d = new DurationCalculator { MaxGapSeconds = 60 }.Calculate(new[] { E("A", 0, null), E("A", 120, null) });
        Assert.Equal(60, d[0].Seconds);
    }

    [Fact]
    public void UsesDurationSeconds()
    {
        var d = new DurationCalculator().Calculate(new[] { E("A", 0, 12d) });
        Assert.Equal(12, d[0].Seconds);
    }

    [Fact]
    public void BuildsSessionsAndIdleAndChains()
    {
        var r = new StatisticsService().Analyze(new[] { E("A", 0, 5), E("A", 5, 5), E("B", 10, 5), E("C", 15, 5, true) });
        Assert.Equal(3, r.Sessions.Count);
        Assert.Equal(15, r.ActiveSeconds);
        Assert.Equal(5, r.IdleSeconds);
        Assert.Contains("A → B", r.Chains2.Keys);
        Assert.DoesNotContain("A → B → C", r.Chains3.Keys);
    }

    [Fact]
    public void ReadsJsonl()
    {
        var d = CreateTempDirectory();
        File.WriteAllText(Path.Combine(d, "events-20260627.jsonl"), JsonEvent("2026-06-27T10:00:00Z", "chrome", "CRM") + Environment.NewLine);

        var result = new LogReader().Read(d);

        var ev = Assert.Single(result.Events);
        Assert.Equal("chrome", ev.ProcessName);
        Assert.Equal("CRM", ev.WindowTitle);
    }

    [Fact]
    public void ReadsCsvWhenJsonlIsMissing()
    {
        var d = CreateTempDirectory();
        File.WriteAllText(Path.Combine(d, "events.csv"), CsvHeader + "2026-06-27T10:00:00Z,m,u,active_window_tick,EXCEL,42,Report,30,shot.png\n");

        var result = new LogReader().Read(d);

        var ev = Assert.Single(result.Events);
        Assert.Equal("EXCEL", ev.ProcessName);
        Assert.Equal("Report", ev.WindowTitle);
        Assert.Equal(30, ev.DurationSeconds);
    }

    [Fact]
    public void ReadsZipArchive()
    {
        var d = CreateTempDirectory();
        CreateZipWithJsonl(Path.Combine(d, "archive.zip"), JsonEvent("2026-06-27T10:01:00Z", "EXCEL", "Report"));

        var result = new LogReader().Read(d);

        var ev = Assert.Single(result.Events);
        Assert.Equal("EXCEL", ev.ProcessName);
        Assert.Equal("Report", ev.WindowTitle);
    }

    [Fact]
    public void PrefersJsonlOverCsvForSamePeriod()
    {
        var d = CreateTempDirectory();
        File.WriteAllText(Path.Combine(d, "events-20260627.jsonl"), JsonEvent("2026-06-27T10:00:00Z", "chrome", "CRM") + Environment.NewLine);
        File.WriteAllText(Path.Combine(d, "events.csv"), CsvHeader + "2026-06-27T10:00:00Z,m,u,active_window_tick,chrome,42,CRM,30,shot.png\n");

        var result = new LogReader().Read(d);

        var ev = Assert.Single(result.Events);
        Assert.Equal("chrome", ev.ProcessName);
        Assert.Equal("CRM", ev.WindowTitle);
        Assert.Equal(0, ev.DuplicatesRemoved);
    }

    [Fact]
    public void DeduplicatesSameEventAcrossSources()
    {
        var d = CreateTempDirectory();
        var json = JsonEvent("2026-06-27T10:00:00Z", "chrome", "CRM");
        File.WriteAllText(Path.Combine(d, "events-20260627.jsonl"), json + Environment.NewLine);
        CreateZipWithJsonl(Path.Combine(d, "archive.zip"), json);

        var result = new LogReader().Read(d);

        var ev = Assert.Single(result.Events);
        Assert.Equal("chrome", ev.ProcessName);
        Assert.True(ev.DuplicatesRemoved >= 1);
    }

    [Fact]
    public void ReadsDistinctEventsAcrossSources()
    {
        var d = CreateTempDirectory();
        File.WriteAllText(Path.Combine(d, "events-20260627.jsonl"), JsonEvent("2026-06-27T10:00:00Z", "chrome", "CRM") + Environment.NewLine);
        File.WriteAllText(Path.Combine(d, "events.csv"), CsvHeader + "2026-06-27T10:00:00Z,m,u,active_window_tick,chrome,42,CRM,30,shot.png\n");
        CreateZipWithJsonl(Path.Combine(d, "archive.zip"), JsonEvent("2026-06-27T10:01:00Z", "EXCEL", "Report"));

        var result = new LogReader().Read(d);

        Assert.Equal(2, result.Events.Count);
        Assert.Contains(result.Events, e => e.ProcessName == "chrome" && e.WindowTitle == "CRM");
        Assert.Contains(result.Events, e => e.ProcessName == "EXCEL" && e.WindowTitle == "Report");
    }

    [Fact]
    public void CreatesReports()
    {
        var d = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var r = new StatisticsService().Analyze(new[] { E("A", 0, 5) });
        Assert.True(File.Exists(new ExcelReportService().Write(r, d, false)));
        Assert.True(File.Exists(new HtmlReportService().Write(r, d)));
    }

    [Fact]
    public void HtmlReportContainsJsonDataAndCyrillicWindowTitle()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var result = new AnalysisResult
        {
            ActiveSeconds = 60,
            IdleSeconds = 0
        };
        result.Sessions.Add(new Session(
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            60,
            "Word",
            "Документ Пример",
            "machine",
            "user",
            0));

        var htmlPath = new HtmlReportService().Write(result, outputDirectory);
        var html = File.ReadAllText(htmlPath);

        Assert.True(File.Exists(htmlPath));
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("report-data", html);
        Assert.Contains("Документ Пример", html);
        Assert.DoesNotContain("__REPORT_JSON__", html);
        Assert.DoesNotContain("__GENERATED_AT__", html);
    }


    [Fact]
    public void LockAppIsLockedAndChromeTicksAreOneSession()
    {
        var r = new StatisticsService().Analyze(new[] { E("chrome",0,5), E("chrome",5,5), E("LockApp",10,5) });
        Assert.Equal(2, r.Sessions.Count);
        Assert.Equal(5, r.LockedSeconds);
        Assert.Empty(r.Chains2);
    }

    [Fact]
    public void AutomationOpportunityHasExplainableScore()
    {
        var r = new StatisticsService().Analyze(new[] { E("A",0,5), E("B",5,5), E("A",10,5), E("B",15,5) });
        Assert.Empty(r.AutomationOpportunities);
    }

    [Fact]
    public void HtmlReportUsesDeterministicRealChartData()
    {
        var result = new StatisticsService().Analyze(new[] { E("chrome",0,30), E("excel",30,30), E("chrome",60,30) });
        var d1 = CreateTempDirectory(); var d2 = CreateTempDirectory();
        var h1 = File.ReadAllText(new HtmlReportService().Write(result, d1));
        var h2 = File.ReadAllText(new HtmlReportService().Write(result, d2));
        Assert.DoesNotContain("Math.random", h1);
        Assert.DoesNotContain("drawPlaceholder", h1);
        Assert.Contains("Диаграмма 1. Структура времени", h1);
        Assert.Contains("chrome", h1);
        Assert.Contains("excel", h1);
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void SessionsDoNotOverlapAndTotalsAreBounded()
    {
        var r = new StatisticsService().Analyze(new[] { E("A",0,60), E("B",10,60), E("B",20,60), E("C",200,60) }, 60);
        foreach (var g in r.Sessions.GroupBy(s => new { s.UserName, s.MachineName }))
        {
            var ordered = g.OrderBy(s => s.Start).ToList();
            for (var i = 0; i < ordered.Count - 1; i++) Assert.True(ordered[i].End <= ordered[i + 1].Start);
        }
        Assert.True(r.ActiveSeconds + r.IdleSeconds + r.LockedSeconds + r.UnknownSeconds <= 180);
        Assert.DoesNotContain(r.Chains2.Keys, k => k.Contains("B → B"));
    }

    [Fact]
    public void XlsxHasNonEmptyChartSeriesAndStructuredSheets()
    {
        var r = new StatisticsService().Analyze(new[] { E("chrome",0,30), E("excel",30,30), E("chrome",60,30) });
        var path = new ExcelReportService().Write(r, CreateTempDirectory(), false);
        using var zip = ZipFile.OpenRead(path);
        var chart = zip.GetEntry("xl/charts/chart1.xml")!;
        using var reader = new StreamReader(chart.Open());
        var xml = reader.ReadToEnd();
        Assert.Contains("<c:ser>", xml);
        Assert.Contains("<c:f>Дашборд!$A$5:$A$8</c:f>", xml);
        Assert.DoesNotContain("<c:plotArea/>", xml);
    }

    [Fact]
    public void InsufficientDataDisablesRecommendations()
    {
        var r = new StatisticsService().Analyze(new[] { E("A",0,5), E("B",5,5) });
        Assert.Equal("red", r.DataQuality!.Level);
        Assert.Empty(r.AutomationOpportunities);
        Assert.Contains("Объём данных недостаточен", r.DataQuality.Warning);
    }


    private const string CsvHeader = "timestampUtc,machineName,userName,eventType,processName,processId,windowTitle,durationSeconds,screenshotFile\n";

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string JsonEvent(string timestampUtc, string processName, string windowTitle) =>
        $"{{\"timestampUtc\":\"{timestampUtc}\",\"machineName\":\"m\",\"userName\":\"u\",\"eventType\":\"active_window_tick\",\"processName\":\"{processName}\",\"processId\":42,\"windowTitle\":\"{windowTitle}\",\"durationSeconds\":30,\"screenshotFile\":\"shot.png\",\"isIdle\":false}}";

    private static void CreateZipWithJsonl(string path, string json)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("events-20260627.jsonl");
        using var writer = new StreamWriter(entry.Open());
        writer.WriteLine(json);
    }

    private static LogEvent E(string p, int sec, double? dur, bool idle = false) =>
        new("active_window_tick", DateTimeOffset.UnixEpoch.AddSeconds(sec), DateTimeOffset.UnixEpoch.AddSeconds(sec), "m", "u", p, 1, p, "", idle, dur, null, null, "test");
}
