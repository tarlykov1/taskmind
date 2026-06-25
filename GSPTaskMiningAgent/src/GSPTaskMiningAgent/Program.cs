using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using GSPTaskMiningAgent;
using GSPTaskMiningAgent.Models;

var debug = HasArg(args, "--debug");
var statusOnly = HasArg(args, "--status");
var stop = HasArg(args, "--stop");
var selfTest = HasArg(args, "--self-test");

if (debug)
{
    AllocConsole();
    Console.WriteLine("GSP Task Mining Agent debug mode started.");
}

var baseDirectory = AppContext.BaseDirectory;
var mutexName = $@"Local\GSPTaskMiningAgent_{Environment.UserName}";
var stopEventName = $@"Local\GSPTaskMiningAgent_Stop_{Environment.UserName}";

if (selfTest)
{
    return RunSelfTest(debug);
}

if (statusOnly)
{
    ShowStatus(baseDirectory, debug);
    return 0;
}

if (stop)
{
    return StopRunningAgent(stopEventName, debug);
}

using var singleInstance = new Mutex(true, mutexName, out var createdNew);
if (!createdNew)
{
    const string alreadyRunning = "GSP Task Mining Agent уже запущен.";
    if (debug)
    {
        Console.WriteLine(alreadyRunning);
    }
    else
    {
        ShowOneShotMessage(alreadyRunning, "GSP Task Mining Agent");
    }

    return 0;
}

EventWaitHandle? stopEvent = null;
AppConfig config;
try
{
    config = AppConfig.LoadOrCreate(baseDirectory);
    EnsureDataDirectories(config.Agent.LogRoot);
    WriteStatusFile(config.Agent.LogRoot, baseDirectory, "RUNNING");
    stopEvent = new EventWaitHandle(false, EventResetMode.ManualReset, stopEventName);
}
catch (Exception ex)
{
    WriteStartupError(baseDirectory, ex);
    return 1;
}

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdown.Cancel();

var stopWatcher = Task.Run(() =>
{
    try
    {
        stopEvent!.WaitOne();
        shutdown.Cancel();
    }
    catch
    {
        // Shutdown signalling must never crash the agent.
    }
});

var logger = new EventLogger(config, debug);
var windowTracker = new WindowTracker();
var activityTracker = new ActivityTracker();
var screenshotService = new ScreenshotService(config);
var archiveService = new ArchiveService(config, logger);

archiveService.ArchivePreviousDays();
logger.Log(CreateEvent("agent_start", WindowSnapshot.Empty(DateTimeOffset.Now), TimeSpan.Zero, isIdle: false, null));
ShowStartupNotification();

WindowSnapshot? currentWindow = null;
var currentWindowStartedAt = DateTimeOffset.Now;
var idle = false;
var longActivityScreenshotTaken = false;

try
{
    while (!shutdown.IsCancellationRequested)
    {
        try
        {
            var snapshot = windowTracker.GetActiveWindow();
            var idleTime = activityTracker.GetIdleTime();
            var isIdleNow = idleTime.TotalSeconds >= config.Agent.IdleThresholdSeconds;
            var windowChanged = currentWindow is null || snapshot.Handle != currentWindow.Handle ||
                !snapshot.ProcessName.Equals(currentWindow.ProcessName, StringComparison.OrdinalIgnoreCase) ||
                !snapshot.WindowTitle.Equals(currentWindow.WindowTitle, StringComparison.Ordinal);

            if (idle != isIdleNow)
            {
                idle = isIdleNow;
                logger.Log(CreateEvent(idle ? "idle_start" : "idle_end", snapshot, DateTimeOffset.Now - currentWindowStartedAt, idle, null));
            }

            if (windowChanged)
            {
                currentWindow = snapshot;
                currentWindowStartedAt = DateTimeOffset.Now;
                longActivityScreenshotTaken = false;

                var screenshotPath = screenshotService.ShouldTake(snapshot, windowChanged: true, TimeSpan.Zero)
                    ? screenshotService.TryCapture(snapshot, logger)
                    : null;

                logger.Log(CreateEvent("window_change", snapshot, TimeSpan.Zero, idle, screenshotPath));
                if (screenshotPath is not null)
                {
                    logger.Log(CreateEvent("screenshot_taken", snapshot, TimeSpan.Zero, idle, screenshotPath));
                }
            }
            else
            {
                var duration = DateTimeOffset.Now - currentWindowStartedAt;
                string? screenshotPath = null;
                if (!longActivityScreenshotTaken && screenshotService.ShouldTake(snapshot, windowChanged: false, duration))
                {
                    screenshotPath = screenshotService.TryCapture(snapshot, logger);
                    longActivityScreenshotTaken = screenshotPath is not null;
                }

                logger.Log(CreateEvent("active_window_tick", snapshot, duration, idle, screenshotPath));
                if (screenshotPath is not null)
                {
                    logger.Log(CreateEvent("screenshot_taken", snapshot, duration, idle, screenshotPath));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Agent loop error");
        }

        await Task.Delay(Math.Max(config.Agent.PollIntervalMs, 250), shutdown.Token).ContinueWith(_ => { });
    }
}
finally
{
    logger.Log(CreateEvent("agent_stop", currentWindow ?? WindowSnapshot.Empty(DateTimeOffset.Now), DateTimeOffset.Now - currentWindowStartedAt, idle, null));
    WriteStatusFile(config.Agent.LogRoot, baseDirectory, "STOPPED");
    stopEvent?.Dispose();
}

return 0;

static bool HasArg(string[] args, string name) => args.Any(arg => arg.Equals(name, StringComparison.OrdinalIgnoreCase));

static ActivityEvent CreateEvent(string type, WindowSnapshot snapshot, TimeSpan duration, bool isIdle, string? screenshotPath) => new()
{
    Timestamp = DateTimeOffset.Now,
    User = Environment.UserName,
    Machine = Environment.MachineName,
    EventType = type,
    ProcessName = string.IsNullOrWhiteSpace(snapshot.ProcessName) ? null : snapshot.ProcessName,
    WindowTitle = string.IsNullOrWhiteSpace(snapshot.WindowTitle) ? null : snapshot.WindowTitle,
    Domain = snapshot.Domain,
    DurationSeconds = Math.Max(0, (long)duration.TotalSeconds),
    IsIdle = isIdle,
    ScreenshotPath = screenshotPath
};

static void EnsureDataDirectories(string dataRoot)
{
    Directory.CreateDirectory(dataRoot);
    Directory.CreateDirectory(Path.Combine(dataRoot, "logs"));
    Directory.CreateDirectory(Path.Combine(dataRoot, "screenshots"));
    Directory.CreateDirectory(Path.Combine(dataRoot, "archives"));
    Directory.CreateDirectory(Path.Combine(dataRoot, "errors"));
}

static void WriteStatusFile(string dataRoot, string baseDirectory, string state)
{
    Directory.CreateDirectory(dataRoot);
    var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
    var lines = new[]
    {
        $"status={state}",
        $"timestamp={DateTimeOffset.Now:O}",
        $"version={version}",
        $"pid={Environment.ProcessId}",
        $"exePath={Environment.ProcessPath ?? Path.Combine(baseDirectory, "GSPTaskMiningAgent.exe")}",
        $"dataRoot={dataRoot}"
    };
    File.WriteAllLines(Path.Combine(dataRoot, "agent-status.txt"), lines);
}

static void ShowStatus(string baseDirectory, bool debug)
{
    try
    {
        var config = AppConfig.LoadOrCreate(baseDirectory);
        EnsureDataDirectories(config.Agent.LogRoot);
        var statusPath = Path.Combine(config.Agent.LogRoot, "agent-status.txt");
        var status = File.Exists(statusPath) ? File.ReadAllText(statusPath) : "agent-status.txt not found.";
        var text = $"GSP Task Mining Agent status\n\nBase folder: {baseDirectory}\nData folder: {config.Agent.LogRoot}\nLogs: {Path.Combine(config.Agent.LogRoot, "logs")}\n\n{status}";

        if (debug)
        {
            Console.WriteLine(text);
        }
        else
        {
            ShowOneShotMessage(text, "GSP Task Mining Agent status");
        }
    }
    catch (Exception ex)
    {
        if (debug)
        {
            Console.WriteLine(ex);
        }
        else
        {
            ShowOneShotMessage(ex.Message, "GSP Task Mining Agent status error");
        }
    }
}

static int StopRunningAgent(string stopEventName, bool debug)
{
    try
    {
        using var existing = EventWaitHandle.OpenExisting(stopEventName);
        existing.Set();
        if (debug)
        {
            Console.WriteLine("Stop signal sent.");
        }
        else
        {
            ShowOneShotMessage("Команда остановки отправлена.", "GSP Task Mining Agent");
        }

        return 0;
    }
    catch (WaitHandleCannotBeOpenedException)
    {
        if (debug)
        {
            Console.WriteLine("Agent is not running.");
        }
        else
        {
            ShowOneShotMessage("GSP Task Mining Agent не запущен.", "GSP Task Mining Agent");
        }

        return 2;
    }
}

static int RunSelfTest(bool debug)
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "GSPTaskMiningAgentSelfTest", Guid.NewGuid().ToString("N"));
    try
    {
        Directory.CreateDirectory(tempRoot);
        var configJson = AppConfig.ReadEmbeddedExampleConfig();
        File.WriteAllText(Path.Combine(tempRoot, "config.json"), configJson);
        var config = AppConfig.LoadOrCreate(tempRoot);
        EnsureDataDirectories(config.Agent.LogRoot);

        var testEvent = CreateEvent("self_test", WindowSnapshot.Empty(DateTimeOffset.Now), TimeSpan.Zero, false, null);
        _ = JsonSerializer.Serialize(testEvent, AppConfig.JsonOptions);
        _ = new EventLogger(config, debug);
        _ = new WindowTracker();
        _ = new ActivityTracker();
        _ = new ScreenshotService(config);
        _ = new ArchiveService(config, new EventLogger(config, debug));

        var requiredDirs = new[]
        {
            Path.Combine(config.Agent.LogRoot, "logs"),
            Path.Combine(config.Agent.LogRoot, "screenshots"),
            Path.Combine(config.Agent.LogRoot, "archives"),
            Path.Combine(config.Agent.LogRoot, "errors")
        };

        if (requiredDirs.Any(dir => !Directory.Exists(dir)))
        {
            throw new InvalidOperationException("Self-test failed: one or more data directories were not created.");
        }

        if (debug)
        {
            Console.WriteLine("SELF-TEST OK");
        }

        return 0;
    }
    catch (Exception ex)
    {
        if (debug)
        {
            Console.WriteLine("SELF-TEST FAILED");
            Console.WriteLine(ex);
        }

        return 1;
    }
    finally
    {
        try
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}

static void WriteStartupError(string baseDirectory, Exception exception)
{
    var message = "GSP Task Mining Agent cannot start. Move GSPTaskMiningAgent.exe to a folder where your user can write files, then run it again.";
    try
    {
        var errorDir = Path.Combine(baseDirectory, "data", "errors");
        Directory.CreateDirectory(errorDir);
        File.AppendAllText(Path.Combine(errorDir, "startup-error.log"), $"{DateTimeOffset.Now:O}\t{exception}\n");
    }
    catch
    {
        ShowOneShotMessage(message, "GSP Task Mining Agent startup error");
    }
}

static void ShowStartupNotification()
{
    var thread = new Thread(() =>
    {
        try
        {
            using var icon = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Visible = true,
                Text = "GSP Task Mining Agent"
            };
            icon.ShowBalloonTip(5000, "GSP Task Mining Agent", "GSP Task Mining Agent запущен", ToolTipIcon.Info);
            Thread.Sleep(5500);
            icon.Visible = false;
        }
        catch
        {
            // Notifications are best-effort only.
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.IsBackground = true;
    thread.Start();
}

static void ShowOneShotMessage(string text, string caption)
{
    var thread = new Thread(() => MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information));
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
}

[DllImport("kernel32.dll")]
static extern bool AllocConsole();
