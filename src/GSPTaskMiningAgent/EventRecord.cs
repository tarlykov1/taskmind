namespace GSPTaskMiningAgent;

public sealed record EventRecord(
    string EventType,
    DateTimeOffset TimestampUtc,
    DateTimeOffset TimestampLocal,
    string MachineName,
    string UserName,
    string ProcessName,
    int ProcessId,
    string WindowTitle,
    string BrowserDomain,
    bool IsIdle,
    double? DurationSeconds,
    string? ScreenshotFile,
    string? ScreenshotReason,
    string? CaseId = null,
    string? ProcessLabel = null,
    string? OperationLabel = null);
