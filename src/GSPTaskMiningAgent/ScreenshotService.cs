using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace GSPTaskMiningAgent;

public sealed class ScreenshotService
{
    private DateTimeOffset _lastCaptureUtc = DateTimeOffset.MinValue;

    public string? CaptureIfDue(AgentPaths paths, AgentConfig config)
    {
        if (!config.EnableScreenshots || !OperatingSystem.IsWindows()) return null;
        var now = DateTimeOffset.UtcNow;
        if (now - _lastCaptureUtc < TimeSpan.FromSeconds(Math.Max(1, config.ScreenshotIntervalSeconds))) return null;
        _lastCaptureUtc = now;

        var bounds = Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty;
        if (bounds.Width <= 0 || bounds.Height <= 0) return null;

        var fileName = $"screenshot-{now:yyyyMMdd-HHmmss}.png";
        var path = Path.Combine(paths.Screenshots, fileName);
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        bitmap.Save(path, ImageFormat.Png);
        return Path.Combine("data", "screenshots", fileName).Replace('\\', '/');
    }
}
