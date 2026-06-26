using System.Security.Cryptography;
using System.Text;

namespace GSPTaskMiningAgent;

public static class WindowTitlePrivacy
{
    public static WindowTitleMode Resolve(AgentConfig config, Action<string>? warn = null)
    {
        if (!string.IsNullOrWhiteSpace(config.Privacy?.WindowTitleMode))
        {
            if (Enum.TryParse<WindowTitleMode>(config.Privacy.WindowTitleMode, true, out var parsed)) return parsed;
            warn?.Invoke($"Unknown privacy.windowTitleMode '{config.Privacy.WindowTitleMode}', using masked.");
            return WindowTitleMode.Masked;
        }
        return config.CaptureWindowTitle == false ? WindowTitleMode.Off : config.MaskWindowTitle || config.MaskWindowTitles ? WindowTitleMode.Masked : WindowTitleMode.Plain;
    }

    public static string Apply(string title, string processName, AgentConfig config, Action<string>? warn = null)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;
        if (config.ExcludedProcesses.Any(p => string.Equals(p, processName, StringComparison.OrdinalIgnoreCase))) return string.Empty;
        if (config.ExcludedTitleContains.Any(p => title.Contains(p, StringComparison.OrdinalIgnoreCase))) return string.Empty;
        return Resolve(config, warn) switch
        {
            WindowTitleMode.Plain => title,
            WindowTitleMode.Off => string.Empty,
            _ => $"masked:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(title))).ToLowerInvariant()[..16]}"
        };
    }
}
