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
            writer.WriteLine("timestampUtc,machineName,userName,processName,windowTitle,isIdle,screenshotFile");
        }
        writer.WriteLine(string.Join(',', Escape(record.TimestampUtc.ToString("O")), Escape(record.MachineName), Escape(record.UserName), Escape(record.ProcessName), Escape(record.WindowTitle), record.IsIdle ? "true" : "false", Escape(record.ScreenshotFile ?? "")));
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
