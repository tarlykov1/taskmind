using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using GSPTaskMiningAgent.Models;

namespace GSPTaskMiningAgent;

public sealed partial class WindowTracker
{
    public WindowSnapshot GetActiveWindow()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return WindowSnapshot.Empty(DateTimeOffset.Now);
        }

        var title = ReadWindowTitle(handle);
        _ = GetWindowThreadProcessId(handle, out var processId);
        var processName = string.Empty;

        try
        {
            using var process = Process.GetProcessById((int)processId);
            processName = process.ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? process.ProcessName
                : process.ProcessName + ".exe";
        }
        catch
        {
            processName = "unknown";
        }

        return new WindowSnapshot(handle, processName, title, TryExtractDomain(processName, title), DateTimeOffset.Now);
    }

    private static string ReadWindowTitle(IntPtr handle)
    {
        var length = GetWindowTextLength(handle);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        _ = GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string? TryExtractDomain(string processName, string title)
    {
        if (!IsBrowser(processName) || string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var match = DomainRegex().Match(title);
        return match.Success ? match.Value.Trim().ToLowerInvariant() : null;
    }

    private static bool IsBrowser(string processName) =>
        processName.Equals("chrome.exe", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals("msedge.exe", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals("firefox.exe", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\b([a-z0-9-]+\.)+[a-z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DomainRegex();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
