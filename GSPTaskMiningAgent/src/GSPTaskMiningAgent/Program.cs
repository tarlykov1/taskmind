using System.Runtime.InteropServices;
using GSPTaskMiningAgent;
using GSPTaskMiningAgent.Models;

var debug = args.Any(arg => arg.Equals("--debug", StringComparison.OrdinalIgnoreCase));
if (debug)
{
    AllocConsole();
    Console.WriteLine("GSP Task Mining Agent debug mode started.");
}

var baseDirectory = AppContext.BaseDirectory;
using var singleInstance = new Mutex(true, @"Local\GSPTaskMiningAgent", out var createdNew);
if (!createdNew)
{
    if (debug)
    {
        Console.WriteLine("GSP Task Mining Agent is already running. Exiting second instance.");
    }

    return;
}

var config = AppConfig.LoadOrCreate(baseDirectory);
Directory.CreateDirectory(config.Agent.LogRoot);
Directory.CreateDirectory(Path.Combine(config.Agent.LogRoot, "logs"));
Directory.CreateDirectory(Path.Combine(config.Agent.LogRoot, "screenshots"));
Directory.CreateDirectory(Path.Combine(config.Agent.LogRoot, "archives"));
Directory.CreateDirectory(Path.Combine(config.Agent.LogRoot, "errors"));

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdown.Cancel();

var logger = new EventLogger(config, debug);
var windowTracker = new WindowTracker();
var activityTracker = new ActivityTracker();
var screenshotService = new ScreenshotService(config);
var archiveService = new ArchiveService(config, logger);

archiveService.ArchivePreviousDays();
logger.Log(CreateEvent("agent_start", WindowSnapshot.Empty(DateTimeOffset.Now), TimeSpan.Zero, isIdle: false, null));

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
}

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

[DllImport("kernel32.dll")]
static extern bool AllocConsole();
