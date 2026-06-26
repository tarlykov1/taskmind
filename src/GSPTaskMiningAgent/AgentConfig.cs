using System.Text.Json;
using System.Text.Json.Serialization;

namespace GSPTaskMiningAgent;

public sealed record AgentConfig
{
    public int PollIntervalSeconds { get; init; } = 5;
    public int IdleThresholdSeconds { get; init; } = 60;
    public bool EnableScreenshots { get; init; } = true;
    public int ScreenshotIntervalSeconds { get; init; } = 300;
    public PrivacyConfig? Privacy { get; init; } = new();
    public bool? CaptureWindowTitle { get; init; }
    public bool MaskWindowTitle { get; init; }
    public bool MaskWindowTitles { get; init; }
    public string[] ExcludedProcesses { get; init; } = Array.Empty<string>();
    public string[] ExcludedTitleContains { get; init; } = Array.Empty<string>();
    public bool HashUserName { get; init; } = true;
    public int ArchiveAfterDays { get; init; } = 7;
    public int RetainArchivesDays { get; init; } = 90;
    public string CsvFileName { get; init; } = "events.csv";

    [JsonIgnore]
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static AgentConfig LoadOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            var created = new AgentConfig();
            File.WriteAllText(path, JsonSerializer.Serialize(created, JsonOptions));
            return created;
        }

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<AgentConfig>(json, JsonOptions) ?? new AgentConfig();
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("privacy", out var privacy) || !privacy.TryGetProperty("windowTitleMode", out _))
        {
            config = config with { Privacy = null };
        }
        return config;
    }
}
