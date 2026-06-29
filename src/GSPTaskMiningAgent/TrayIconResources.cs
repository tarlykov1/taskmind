using System.Drawing;
using System.Reflection;

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

    public static Icon Load(TrayIconState state)
    {
        var name = state switch
        {
            TrayIconState.Green => "GSPTaskMiningGreen.ico",
            TrayIconState.Yellow => "GSPTaskMiningYellow.ico",
            TrayIconState.Red => "GSPTaskMiningRed.ico",
            TrayIconState.Gray => "GSPTaskMiningGray.ico",
            _ => "GSPTaskMining.ico"
        };

        return LoadIcon(name);
    }

    public static Icon LoadMain() => LoadIcon("GSPTaskMining.ico");

    private static Icon LoadIcon(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = ResourcePrefix + fileName;
        var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Embedded tray icon resource is missing: " + resourceName);
        return new Icon(stream);
    }
}
