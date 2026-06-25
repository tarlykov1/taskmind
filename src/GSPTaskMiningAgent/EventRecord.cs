namespace GSPTaskMiningAgent;

public sealed record EventRecord(
    DateTimeOffset TimestampUtc,
    string MachineName,
    string UserName,
    string ProcessName,
    string WindowTitle,
    bool IsIdle);
