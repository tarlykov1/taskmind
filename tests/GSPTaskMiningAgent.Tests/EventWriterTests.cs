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
        var record = new EventRecord(DateTimeOffset.UnixEpoch, "machine", "user", "proc", "title, with comma", false, "data/screenshots/a.png");
        CsvEventWriter.Append(file, record);
        var lines = File.ReadAllLines(file);
        Assert.Equal("timestampUtc,machineName,userName,processName,windowTitle,isIdle,screenshotFile", lines[0]);
        Assert.Contains("\"title, with comma\"", lines[1]);
    }
}
