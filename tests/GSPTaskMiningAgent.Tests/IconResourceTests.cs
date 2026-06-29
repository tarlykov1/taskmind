using System.Drawing;
using Xunit;

namespace GSPTaskMiningAgent.Tests;

public sealed class IconResourceTests
{
    private static readonly string[] IconFiles =
    [
        "GSPTaskMining.ico",
        "GSPTaskMiningGreen.ico",
        "GSPTaskMiningYellow.ico",
        "GSPTaskMiningRed.ico",
        "GSPTaskMiningGray.ico"
    ];

    private static readonly TrayIconState[] TrayStates =
    [
        TrayIconState.Green,
        TrayIconState.Yellow,
        TrayIconState.Red,
        TrayIconState.Gray
    ];

    [Fact]
    public void IconFilesExistAndAreValidIcoFiles()
    {
        foreach (var iconFile in IconFiles)
        {
            var path = Path.Combine(GetRepositoryRoot(), "src", "GSPTaskMiningAgent", "Assets", iconFile);
            Assert.True(File.Exists(path), "Icon not found: " + path);
            Assert.True(new FileInfo(path).Length > 0, "Icon is empty: " + path);
            using var icon = new Icon(path);
            Assert.True(icon.Width > 0, "Invalid icon width: " + path);
            Assert.True(icon.Height > 0, "Invalid icon height: " + path);
            Assert.Equal(new[] { 16, 20, 24, 32, 48, 256 }, ReadIcoSizes(path));
        }
    }

    [Fact]
    public void StatusIconResourcesAreEmbeddedInAssembly()
    {
        var resources = typeof(AgentConfig).Assembly.GetManifestResourceNames();
        foreach (var state in TrayStates) Assert.Contains(TrayIconResources.GetResourceName(state), resources);
    }

    [Fact]
    public void TrayIconResourcesLoadsEveryStatus()
    {
        foreach (var state in TrayStates)
        {
            using var icon = TrayIconResources.LoadRequired(state);
            Assert.True(icon.Width > 0);
            Assert.True(icon.Height > 0);
        }
    }

    [Fact]
    public void TrayApplicationContextDoesNotUseSystemIcons()
    {
        foreach (var sourceFile in Directory.EnumerateFiles(Path.Combine(GetRepositoryRoot(), "src", "GSPTaskMiningAgent"), "*.cs"))
        {
            var source = File.ReadAllText(sourceFile);
            Assert.DoesNotContain("SystemIcons.Application", source);
            Assert.DoesNotContain("SystemIcons.Information", source);
            Assert.DoesNotContain("SystemIcons.Warning", source);
            Assert.DoesNotContain("SystemIcons.Error", source);
        }
    }

    [Fact]
    public void ProjectUsesCommittedIconsWithoutBuildTimeGeneration()
    {
        var project = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "GSPTaskMiningAgent", "GSPTaskMiningAgent.csproj"));
        Assert.Contains("<ApplicationIcon>Assets\\GSPTaskMining.ico</ApplicationIcon>", project);
        Assert.Contains("<EmbeddedResource Include=\"Assets\\GSPTaskMiningGreen.ico\" />", project);
        Assert.Contains("<EmbeddedResource Include=\"Assets\\GSPTaskMiningYellow.ico\" />", project);
        Assert.Contains("<EmbeddedResource Include=\"Assets\\GSPTaskMiningRed.ico\" />", project);
        Assert.Contains("<EmbeddedResource Include=\"Assets\\GSPTaskMiningGray.ico\" />", project);
        Assert.DoesNotContain("GenerateBrandedIcons", project);
        Assert.DoesNotContain("generate-icons.ps1", project);
        Assert.DoesNotContain("<EmbeddedResource Include=\"Assets\\GSPTaskMining.ico\" />", project);
    }

    private static int[] ReadIcoSizes(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        Assert.Equal(0, reader.ReadUInt16());
        Assert.Equal(1, reader.ReadUInt16());
        var count = reader.ReadUInt16();
        var sizes = new List<int>();
        for (var i = 0; i < count; i++)
        {
            var width = reader.ReadByte();
            _ = reader.ReadByte();
            reader.BaseStream.Seek(14, SeekOrigin.Current);
            sizes.Add(width == 0 ? 256 : width);
        }
        return sizes.Order().ToArray();
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GSPTaskMiningAgent.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
