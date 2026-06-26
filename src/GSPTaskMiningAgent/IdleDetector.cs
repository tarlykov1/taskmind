using System.Runtime.InteropServices;

namespace GSPTaskMiningAgent;

public static class IdleDetector
{
    public static bool IsIdle(TimeSpan threshold)
    {
        if (!OperatingSystem.IsWindows()) return false;
        var info = new LastInputInfo { CbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return false;
        var idleMilliseconds = unchecked((uint)Environment.TickCount - info.DwTime);
        return idleMilliseconds >= threshold.TotalMilliseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint CbSize;
        public uint DwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo plii);
}
