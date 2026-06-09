namespace GSPTaskMiningAgent.Models;

public sealed class ActivityEvent
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string User { get; set; } = string.Empty;
    public string Machine { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? ProcessName { get; set; }
    public string? WindowTitle { get; set; }
    public string? Domain { get; set; }
    public long DurationSeconds { get; set; }
    public bool IsIdle { get; set; }
    public string? ScreenshotPath { get; set; }
    public string? Message { get; set; }
}
