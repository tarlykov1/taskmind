using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GSPTaskMiningAgent;

internal static class Program
{
    private const int MaxSelfTestSeconds = 20;

    public static int Main(string[] args)
    {
        var root = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var paths = new AgentPaths(root);

        try
        {
            if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
            {
                return RunSelfTest(paths);
            }

            if (args.Contains("--stop", StringComparer.OrdinalIgnoreCase))
            {
                paths.EnsureAll();
                File.WriteAllText(paths.StopFile, DateTimeOffset.UtcNow.ToString("O"));
                return 0;
            }

            return Run(paths, args.Contains("--once", StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            paths.EnsureAll();
            File.WriteAllText(Path.Combine(paths.Errors, $"fatal-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.log"), ex.ToString());
            return 1;
        }
    }

    private static int Run(AgentPaths paths, bool once)
    {
        paths.EnsureAll();
        var config = AgentConfig.LoadOrCreate(paths.ConfigFile);
        if (File.Exists(paths.StopFile)) File.Delete(paths.StopFile);

        do
        {
            WriteStatus(paths, "running");
            var record = CaptureEvent(config);
            AppendEvent(paths, record);
            if (once) break;
            Thread.Sleep(TimeSpan.FromSeconds(Math.Max(1, config.PollIntervalSeconds)));
        } while (!File.Exists(paths.StopFile));

        WriteStatus(paths, "stopped");
        return 0;
    }

    private static int RunSelfTest(AgentPaths paths)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(MaxSelfTestSeconds);
        paths.EnsureAll();
        _ = AgentConfig.LoadOrCreate(paths.ConfigFile);
        AppendEvent(paths, CaptureEvent(new AgentConfig()));
        WriteStatus(paths, "self-test-ok");

        var checks = new[] { paths.ConfigFile, paths.StatusFile };
        var directories = new[] { paths.Logs, paths.Screenshots, paths.Archives, paths.Errors };
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (checks.All(File.Exists) && directories.All(Directory.Exists) && Directory.EnumerateFiles(paths.Logs, "*.jsonl").Any())
            {
                return 0;
            }
            Thread.Sleep(100);
        }

        return 2;
    }

    private static EventRecord CaptureEvent(AgentConfig config)
    {
        var processName = "unknown";
        var title = "";
        if (OperatingSystem.IsWindows())
        {
            var handle = GetForegroundWindow();
            if (handle != IntPtr.Zero)
            {
                var pid = 0u;
                _ = GetWindowThreadProcessId(handle, out pid);
                try { processName = Process.GetProcessById((int)pid).ProcessName; } catch { processName = "unknown"; }
                var builder = new StringBuilder(512);
                _ = GetWindowText(handle, builder, builder.Capacity);
                title = builder.ToString();
            }
        }

        var user = Environment.UserName;
        if (config.HashUserName)
        {
            user = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user))).ToLowerInvariant();
        }

        if (config.MaskWindowTitles && title.Length > 0)
        {
            title = $"masked:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(title))).ToLowerInvariant()[..16]}";
        }

        return new EventRecord(DateTimeOffset.UtcNow, Environment.MachineName, user, processName, title, false);
    }

    private static void AppendEvent(AgentPaths paths, EventRecord record)
    {
        var file = Path.Combine(paths.Logs, $"events-{DateTimeOffset.UtcNow:yyyyMMdd}.jsonl");
        File.AppendAllText(file, JsonSerializer.Serialize(record, AgentConfig.JsonOptions).ReplaceLineEndings("") + Environment.NewLine);
    }

    private static void WriteStatus(AgentPaths paths, string state)
    {
        File.WriteAllText(paths.StatusFile, $"state={state}{Environment.NewLine}utc={DateTimeOffset.UtcNow:O}{Environment.NewLine}");
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
