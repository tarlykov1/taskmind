using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using GSPTaskMiningAgent.Models;

namespace GSPTaskMiningAgent;

public sealed class ScreenshotService
{
    private readonly AppConfig _config;
    private int _screenshotsToday;
    private DateOnly _counterDate = DateOnly.FromDateTime(DateTime.Today);

    public ScreenshotService(AppConfig config)
    {
        _config = config;
        Directory.CreateDirectory(Path.Combine(_config.Agent.LogRoot, "screenshots"));
    }

    public bool ShouldTake(WindowSnapshot snapshot, bool windowChanged, TimeSpan activeDuration)
    {
        if (!_config.Agent.EnableScreenshots || IsDailyLimitReached())
        {
            return false;
        }

        var processAllowed = _config.ScreenshotRules.AllowedProcesses.Any(p => p.Equals(snapshot.ProcessName, StringComparison.OrdinalIgnoreCase));
        var titleAllowed = _config.ScreenshotRules.TitleContains.Any(t => snapshot.WindowTitle.Contains(t, StringComparison.OrdinalIgnoreCase));
        var triggerAllowed = (_config.ScreenshotRules.TakeOnWindowChange && windowChanged) ||
            (_config.ScreenshotRules.TakeOnLongActivitySeconds > 0 && activeDuration.TotalSeconds >= _config.ScreenshotRules.TakeOnLongActivitySeconds);

        return triggerAllowed && (processAllowed || titleAllowed);
    }

    public string? TryCapture(WindowSnapshot snapshot, EventLogger logger)
    {
        if (!_config.Agent.EnableScreenshots || IsDailyLimitReached())
        {
            return null;
        }

        try
        {
            ResetCounterIfNeeded();
            var date = DateTime.Now.ToString("yyyy-MM-dd");
            var directory = Path.Combine(_config.Agent.LogRoot, "screenshots", date);
            Directory.CreateDirectory(directory);

            var safeProcess = SanitizeFileName(Path.GetFileNameWithoutExtension(snapshot.ProcessName));
            var fileName = $"{DateTime.Now:HH-mm-ss}_{safeProcess}.jpg";
            var fullPath = Path.Combine(directory, fileName);

            var bounds = GetVirtualScreenBounds();
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
            }

            SaveJpeg(bitmap, fullPath, Math.Clamp(_config.Agent.ScreenshotQuality, 1, 100));
            _screenshotsToday++;
            return Path.GetRelativePath(_config.Agent.LogRoot, fullPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to capture screenshot");
            return null;
        }
    }

    private bool IsDailyLimitReached()
    {
        ResetCounterIfNeeded();
        return _screenshotsToday >= _config.Agent.MaxScreenshotsPerDay;
    }

    private void ResetCounterIfNeeded()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (today != _counterDate)
        {
            _counterDate = today;
            _screenshotsToday = 0;
        }
    }

    private static Rectangle GetVirtualScreenBounds()
    {
        var left = GetSystemMetrics(SystemMetric.XVirtualScreen);
        var top = GetSystemMetrics(SystemMetric.YVirtualScreen);
        var width = GetSystemMetrics(SystemMetric.CxVirtualScreen);
        var height = GetSystemMetrics(SystemMetric.CyVirtualScreen);
        return new Rectangle(left, top, width, height);
    }

    private static void SaveJpeg(Bitmap bitmap, string path, int quality)
    {
        var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
        bitmap.Save(path, codec, parameters);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim('_');
        return string.IsNullOrWhiteSpace(clean) ? "window" : clean;
    }

    private enum SystemMetric
    {
        XVirtualScreen = 76,
        YVirtualScreen = 77,
        CxVirtualScreen = 78,
        CyVirtualScreen = 79
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(SystemMetric smIndex);
}
