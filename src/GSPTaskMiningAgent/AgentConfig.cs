using System.Text.Json;
using System.Text.Json.Serialization;

namespace GSPTaskMiningAgent;

public sealed record AgentConfig
{
    public int PollIntervalSeconds { get; init; } = 5;
    public int IdleThresholdSeconds { get; init; } = 60;
    public bool EnableScreenshots { get; init; } = true;
    public int ScreenshotIntervalSeconds { get; init; } = 300;
    public bool MaskWindowTitles { get; init; } = true;
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
        return JsonSerializer.Deserialize<AgentConfig>(json, JsonOptions) ?? new AgentConfig();
    }
}
