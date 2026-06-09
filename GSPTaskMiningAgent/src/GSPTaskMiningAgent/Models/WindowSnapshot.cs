namespace GSPTaskMiningAgent.Models;

public sealed record WindowSnapshot(
    IntPtr Handle,
    string ProcessName,
    string WindowTitle,
    string? Domain,
    DateTimeOffset SeenAt)
{
    public static WindowSnapshot Empty(DateTimeOffset seenAt) => new(IntPtr.Zero, string.Empty, string.Empty, null, seenAt);
}
