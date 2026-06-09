using System.Text.Json;
using System.Text.Json.Serialization;

namespace GSPTaskMiningAgent;

public sealed class AppConfig
{
    public AgentOptions Agent { get; set; } = new();
    public PrivacyOptions Privacy { get; set; } = new();
    public ScreenshotRuleOptions ScreenshotRules { get; set; } = new();
    public LoggingOptions Logging { get; set; } = new();

    public static AppConfig LoadOrCreate(string baseDirectory)
    {
        var configPath = Path.Combine(baseDirectory, "config.json");
        var examplePath = Path.Combine(baseDirectory, "config.example.json");

        if (!File.Exists(configPath))
        {
            if (File.Exists(examplePath))
            {
                File.Copy(examplePath, configPath);
            }
            else
            {
                var defaults = new AppConfig();
                File.WriteAllText(configPath, JsonSerializer.Serialize(defaults, JsonOptions));
            }
        }

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        config.NormalizePaths(baseDirectory);
        return config;
    }

    private void NormalizePaths(string baseDirectory)
    {
        Agent.LogRoot = ResolvePortablePath(baseDirectory, Agent.LogRoot);
    }

    private static string ResolvePortablePath(string baseDirectory, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Path.Combine(baseDirectory, "data");
        }

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public sealed class AgentOptions
{
    public int PollIntervalMs { get; set; } = 1000;
    public int IdleThresholdSeconds { get; set; } = 60;
    public string LogRoot { get; set; } = @"data";
    public bool EnableScreenshots { get; set; } = true;
    public int ScreenshotQuality { get; set; } = 75;
    public int MaxScreenshotsPerDay { get; set; } = 300;
    public bool ArchiveDaily { get; set; } = true;
    public bool CopyArchiveToNetworkShare { get; set; }
    public string NetworkSharePath { get; set; } = @"\\fileserver\task_mining_pilot";
}

public sealed class PrivacyOptions
{
    public bool HashUserName { get; set; }
    public bool MaskWindowTitles { get; set; }
    public bool AllowedDomainsOnly { get; set; }
}

public sealed class ScreenshotRuleOptions
{
    public bool TakeOnWindowChange { get; set; } = true;
    public int TakeOnLongActivitySeconds { get; set; } = 120;
    public string[] AllowedProcesses { get; set; } =
    [
        "chrome.exe", "msedge.exe", "1cv8.exe", "EXCEL.EXE", "WINWORD.EXE", "OUTLOOK.EXE"
    ];
    public string[] TitleContains { get; set; } =
    [
        "Naumen", "Service Desk", "Битрикс24", "1С", "Заявка", "Документ"
    ];
}

public sealed class LoggingOptions
{
    public bool WriteJsonl { get; set; } = true;
    public bool WriteCsv { get; set; } = true;
    public bool RotateDaily { get; set; } = true;
}
