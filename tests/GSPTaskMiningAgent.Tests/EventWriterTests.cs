using Xunit;
using GSPTaskMiningAgent;

namespace GSPTaskMiningAgent.Tests;

public sealed class EventWriterTests
{
    [Fact]
    public void CsvWriterCreatesHeaderAndEscapesValues()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "events.csv");
        var record = new EventRecord("active_window_tick", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "machine", "user", "proc", 123, "title, with comma", "", false, 5, "data/screenshots/a.png", "interval");
        CsvEventWriter.Append(file, record);
        var lines = File.ReadAllLines(file);
        Assert.Equal("eventType,timestampUtc,timestampLocal,machineName,userName,processName,processId,windowTitle,browserDomain,isIdle,durationSeconds,screenshotFile,screenshotReason", lines[0]);
        Assert.Contains("\"title, with comma\"", lines[1]);
    }
}
