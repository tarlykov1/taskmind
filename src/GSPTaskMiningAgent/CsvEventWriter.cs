using System.Text;

namespace GSPTaskMiningAgent;

public static class CsvEventWriter
{
    public static void Append(string path, EventRecord record)
    {
        var isNew = !File.Exists(path);
        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        if (isNew)
        {
            writer.WriteLine("eventType,timestampUtc,timestampLocal,machineName,userName,processName,processId,windowTitle,browserDomain,isIdle,durationSeconds,screenshotFile,screenshotReason");
        }
        writer.WriteLine(string.Join(',', Escape(record.EventType), Escape(record.TimestampUtc.ToString("O")), Escape(record.TimestampLocal.ToString("O")), Escape(record.MachineName), Escape(record.UserName), Escape(record.ProcessName), record.ProcessId.ToString(), Escape(record.WindowTitle), Escape(record.BrowserDomain), record.IsIdle ? "true" : "false", record.DurationSeconds?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "", Escape(record.ScreenshotFile ?? ""), Escape(record.ScreenshotReason ?? "")));
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
