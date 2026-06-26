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
        Assert.Equal(0, d[0].Seconds);
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
        Assert.Contains("A → B → C", r.Chains3.Keys);
    }

    [Fact]
    public void ReadsJsonlCsvAndZip()
    {
        var d = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        var json = "{\"timestampUtc\":\"2026-01-01T00:00:00Z\",\"machineName\":\"m\",\"userName\":\"u\",\"processName\":\"A\",\"isIdle\":false}\n";
        File.WriteAllText(Path.Combine(d, "a.jsonl"), json);
        File.WriteAllText(Path.Combine(d, "b.csv"), "timestampUtc,machineName,userName,processName,isIdle\n2026-01-01T00:00:01Z,m,u,B,false\n");
        var zf = Path.Combine(d, "c.zip");
        using (var z = ZipFile.Open(zf, ZipArchiveMode.Create))
        {
            var e = z.CreateEntry("c.jsonl");
            using var w = new StreamWriter(e.Open());
            w.Write(json);
        }

        var r = new LogReader().Read(d);
        Assert.Equal(3, r.Events.Count);
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

    private static LogEvent E(string p, int sec, double? dur, bool idle = false) =>
        new("active_window_tick", DateTimeOffset.UnixEpoch.AddSeconds(sec), DateTimeOffset.UnixEpoch.AddSeconds(sec), "m", "u", p, 1, p, "", idle, dur, null, null, "test");
}
