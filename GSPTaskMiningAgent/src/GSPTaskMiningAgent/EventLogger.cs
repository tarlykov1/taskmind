using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using GSPTaskMiningAgent.Models;

namespace GSPTaskMiningAgent;

public sealed class EventLogger
{
    private readonly AppConfig _config;
    private readonly bool _debug;
    private readonly object _sync = new();
    private readonly JsonSerializerOptions _jsonOptions = new(AppConfig.JsonOptions)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public EventLogger(AppConfig config, bool debug)
    {
        _config = config;
        _debug = debug;
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(ErrorsDirectory);
    }

    public string LogsDirectory => Path.Combine(_config.Agent.LogRoot, "logs");
    public string ErrorsDirectory => Path.Combine(_config.Agent.LogRoot, "errors");
    public string ErrorLogPath => Path.Combine(ErrorsDirectory, "agent_errors.log");

    public void Log(ActivityEvent activityEvent)
    {
        activityEvent.User = ApplyUserPrivacy(activityEvent.User);
        activityEvent.WindowTitle = ApplyTitlePrivacy(activityEvent.WindowTitle);

        lock (_sync)
        {
            Directory.CreateDirectory(LogsDirectory);
            var date = activityEvent.Timestamp.ToString("yyyy-MM-dd");

            if (_config.Logging.WriteJsonl)
            {
                File.AppendAllText(
                    Path.Combine(LogsDirectory, $"{date}_events.jsonl"),
                    JsonSerializer.Serialize(activityEvent, _jsonOptions) + Environment.NewLine,
                    Encoding.UTF8);
            }

            if (_config.Logging.WriteCsv)
            {
                var csvPath = Path.Combine(LogsDirectory, $"{date}_windows.csv");
                if (!File.Exists(csvPath))
                {
                    File.AppendAllText(csvPath, "timestamp,user,machine,eventType,processName,windowTitle,domain,durationSeconds,isIdle,screenshotPath" + Environment.NewLine, Encoding.UTF8);
                }

                File.AppendAllText(csvPath, ToCsv(activityEvent) + Environment.NewLine, Encoding.UTF8);
            }
        }

        if (_debug)
        {
            Console.WriteLine($"{activityEvent.Timestamp:O} {activityEvent.EventType} {activityEvent.ProcessName} {activityEvent.WindowTitle} idle={activityEvent.IsIdle} screenshot={activityEvent.ScreenshotPath}");
        }
    }

    public void LogError(Exception exception, string context)
    {
        try
        {
            Directory.CreateDirectory(ErrorsDirectory);
            File.AppendAllText(ErrorLogPath, $"{DateTimeOffset.Now:O}\t{context}\t{exception}\n", Encoding.UTF8);
            Log(new ActivityEvent
            {
                Timestamp = DateTimeOffset.Now,
                User = Environment.UserName,
                Machine = Environment.MachineName,
                EventType = "error",
                Message = context + ": " + exception.Message
            });
        }
        catch
        {
            // Last-resort logging must never crash the agent.
        }
    }

    private string ApplyUserPrivacy(string user)
    {
        if (!_config.Privacy.HashUserName || string.IsNullOrWhiteSpace(user))
        {
            return user;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(user));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private string? ApplyTitlePrivacy(string? title) =>
        _config.Privacy.MaskWindowTitles && !string.IsNullOrEmpty(title) ? "[masked]" : title;

    private static string ToCsv(ActivityEvent activityEvent)
    {
        var fields = new[]
        {
            activityEvent.Timestamp.ToString("O"), activityEvent.User, activityEvent.Machine, activityEvent.EventType,
            activityEvent.ProcessName ?? string.Empty, activityEvent.WindowTitle ?? string.Empty, activityEvent.Domain ?? string.Empty,
            activityEvent.DurationSeconds.ToString(), activityEvent.IsIdle.ToString(), activityEvent.ScreenshotPath ?? string.Empty
        };
        return string.Join(',', fields.Select(EscapeCsv));
    }

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
}
