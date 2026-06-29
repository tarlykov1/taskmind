using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GSPTaskMiningAgent;

internal enum TrayIconState
{
    Green,
    Yellow,
    Red,
    Gray
}

internal static class TrayIconResources
{
    private const string ResourcePrefix = "GSPTaskMiningAgent.Assets.";
    private static string? _errorDirectory;

    public static void ConfigureErrorDirectory(string errorDirectory) => _errorDirectory = errorDirectory;

    public static Icon Load(TrayIconState state)
    {
        try
        {
            return LoadRequired(state);
        }
        catch (Exception ex)
        {
            LogIconError(ex);
            return CreateFallbackIcon();
        }
    }

    public static Icon LoadRequired(TrayIconState state)
    {
        var resourceName = ResourcePrefix + GetFileName(state);
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Embedded tray icon resource is missing: " + resourceName);
        return new Icon(stream);
    }

    public static string GetResourceName(TrayIconState state) => ResourcePrefix + GetFileName(state);

    private static string GetFileName(TrayIconState state) => state switch
    {
        TrayIconState.Green => "GSPTaskMiningGreen.ico",
        TrayIconState.Yellow => "GSPTaskMiningYellow.ico",
        TrayIconState.Red => "GSPTaskMiningRed.ico",
        TrayIconState.Gray => "GSPTaskMiningGray.ico",
        _ => "GSPTaskMiningGreen.ico"
    };

    private static void LogIconError(Exception ex)
    {
        if (string.IsNullOrWhiteSpace(_errorDirectory)) return;
        try
        {
            Directory.CreateDirectory(_errorDirectory);
            File.WriteAllText(
                Path.Combine(_errorDirectory, $"tray-icon-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.log"),
                ex.ToString());
        }
        catch
        {
            // Ignore secondary logging failures to keep tray startup safe.
        }
    }

    private static Icon CreateFallbackIcon()
    {
        using var bitmap = new Bitmap(16, 16);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(15, 42, 85));
            graphics.FillRectangle(brush, 0, 0, 16, 16);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            _ = DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
