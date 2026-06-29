using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GSPTaskMiningAgent;

internal static class Program
{
    private const int MaxSelfTestSeconds = 20;

    [STAThread]
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

            using var guard = new SingleInstanceGuard();
            if (!guard.IsOwner)
            {
                Console.Error.WriteLine("Агент уже запущен");
                return 3;
            }
            if (args.Contains("--tray-self-test", StringComparer.OrdinalIgnoreCase)) return RunTraySelfTest(paths);
            if (OperatingSystem.IsWindows() && !args.Contains("--once", StringComparer.OrdinalIgnoreCase))
            {
                System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.SystemAware);
                System.Windows.Forms.Application.EnableVisualStyles();
                System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                using var context = new TrayApplicationContext(paths, args);
                System.Windows.Forms.Application.Run(context);
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

    internal static int RunAgentLoop(AgentPaths paths, bool once, CancellationToken cancellationToken) => Run(paths, once, cancellationToken);

    private static int Run(AgentPaths paths, bool once, CancellationToken cancellationToken = default)
    {
        paths.EnsureAll();
        var config = AgentConfig.LoadOrCreate(paths.ConfigFile);
        var screenshots = new ScreenshotService();
        if (File.Exists(paths.StopFile)) File.Delete(paths.StopFile);
        AppendEvent(paths, config, SystemEvent("agent_start", config));

        do
        {
            WriteStatus(paths, "running");
            var screenshotFile = screenshots.CaptureIfDue(paths, config);
            var record = CaptureEvent(paths, config, screenshotFile);
            AppendEvent(paths, config, record);
            ArchiveService.Run(paths, config, DateTimeOffset.UtcNow);
            if (once) break;
            Thread.Sleep(TimeSpan.FromSeconds(Math.Max(1, config.PollIntervalSeconds)));
        } while (!File.Exists(paths.StopFile) && !cancellationToken.IsCancellationRequested);

        AppendEvent(paths, config, SystemEvent("agent_stop", config));
        WriteStatus(paths, "stopped");
        return 0;
    }

    private static int RunSelfTest(AgentPaths paths)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(MaxSelfTestSeconds);
        paths.EnsureAll();
        _ = AgentConfig.LoadOrCreate(paths.ConfigFile);
        var config = new AgentConfig();
        AppendEvent(paths, config, CaptureEvent(paths, config, null));
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

    private static int RunTraySelfTest(AgentPaths paths)
    {
        paths.EnsureAll();
        var file = Path.Combine(paths.Errors, "tray-self-test.txt");
        File.WriteAllText(file, $"STA={Thread.CurrentThread.GetApartmentState()}\nmessageLoop=started\nicon=created\n");
        if (OperatingSystem.IsWindows())
        {
            var timer = new System.Windows.Forms.Timer { Interval = 5000 };
            timer.Tick += (_, _) => System.Windows.Forms.Application.ExitThread();
            timer.Start();
            using var icon = new System.Windows.Forms.NotifyIcon { Icon = TrayIconResources.Load(TrayIconState.Green), Visible = true, Text = "GSP tray self-test" };
            System.Windows.Forms.Application.Run();
        }
        return 0;
    }

    private static EventRecord SystemEvent(string eventType, AgentConfig config)
    {
        var now = DateTimeOffset.UtcNow;
        var user = config.HashUserName ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName))).ToLowerInvariant() : Environment.UserName;
        return new EventRecord(eventType, now, now.ToLocalTime(), Environment.MachineName, user, string.Empty, 0, string.Empty, string.Empty, false, null, null, null);
    }

    private static EventRecord CaptureEvent(AgentPaths paths, AgentConfig config, string? screenshotFile)
    {
        var processName = "unknown";
        var title = "";
        var processId = 0;
        if (OperatingSystem.IsWindows())
        {
            var handle = GetForegroundWindow();
            if (handle != IntPtr.Zero)
            {
                var pid = 0u;
                _ = GetWindowThreadProcessId(handle, out pid);
                processId = (int)pid;
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

        title = WindowTitlePrivacy.Apply(title, processName, config, warning => LogWarning(paths, warning));

        var isIdle = IdleDetector.IsIdle(TimeSpan.FromSeconds(Math.Max(1, config.IdleThresholdSeconds)));
        var now = DateTimeOffset.UtcNow;
        return new EventRecord("active_window_tick", now, now.ToLocalTime(), Environment.MachineName, user, processName, processId, title, ExtractBrowserDomain(title), isIdle, Math.Max(1, config.PollIntervalSeconds), screenshotFile, screenshotFile is null ? null : "interval");
    }

    private static void AppendEvent(AgentPaths paths, AgentConfig config, EventRecord record)
    {
        var file = Path.Combine(paths.Logs, $"events-{DateTimeOffset.UtcNow:yyyyMMdd}.jsonl");
        File.AppendAllText(file, JsonSerializer.Serialize(record, AgentConfig.JsonOptions).ReplaceLineEndings("") + Environment.NewLine);
        CsvEventWriter.Append(Path.Combine(paths.Logs, config.CsvFileName), record);
    }

    private static void WriteStatus(AgentPaths paths, string state)
    {
        var config = AgentConfig.LoadOrCreate(paths.ConfigFile);
        File.WriteAllText(paths.StatusFile, $"state={state}{Environment.NewLine}utc={DateTimeOffset.UtcNow:O}{Environment.NewLine}WINDOW_TITLE_MODE={WindowTitlePrivacy.Resolve(config).ToString().ToLowerInvariant()}{Environment.NewLine}");
    }

    private static void LogWarning(AgentPaths paths, string warning)
    {
        paths.EnsureAll();
        File.AppendAllText(Path.Combine(paths.Errors, $"warnings-{DateTimeOffset.UtcNow:yyyyMMdd}.log"), $"{DateTimeOffset.UtcNow:O} {warning}{Environment.NewLine}");
    }

    private static string ExtractBrowserDomain(string title)
    {
        var match = System.Text.RegularExpressions.Regex.Match(title, @"https?://(?<host>[a-z0-9.-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["host"].Value.ToLowerInvariant() : string.Empty;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
