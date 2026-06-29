using System.Reflection;
using Xunit;

namespace GSPTaskMiningAgent.Tests;

public sealed class IconResourceTests
{
    private static readonly string[] IconResources =
    [
        "GSPTaskMiningAgent.Assets.GSPTaskMining.ico",
        "GSPTaskMiningAgent.Assets.GSPTaskMiningGreen.ico",
        "GSPTaskMiningAgent.Assets.GSPTaskMiningYellow.ico",
        "GSPTaskMiningAgent.Assets.GSPTaskMiningRed.ico",
        "GSPTaskMiningAgent.Assets.GSPTaskMiningGray.ico"
    ];

    [Fact]
    public void IconResourcesAreEmbeddedInAssembly()
    {
        var resources = typeof(AgentConfig).Assembly.GetManifestResourceNames();
        foreach (var icon in IconResources) Assert.Contains(icon, resources);
    }

    [Fact]
    public void TrayApplicationContextDoesNotUseSystemApplicationIcon()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "GSPTaskMiningAgent", "TrayApplicationContext.cs"));
        var selfTest = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "GSPTaskMiningAgent", "Program.cs"));
        Assert.DoesNotContain("SystemIcons.Application", source);
        Assert.DoesNotContain("System.Drawing.SystemIcons.Application", selfTest);
    }

    [Fact]
    public void ApplicationIconIsConfiguredInProjectFile()
    {
        var project = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "GSPTaskMiningAgent", "GSPTaskMiningAgent.csproj"));
        Assert.Contains("<ApplicationIcon>Assets\\GSPTaskMining.ico</ApplicationIcon>", project);
    }

    [Fact]
    public void EachIconResourceCanBeRead()
    {
        var assembly = typeof(AgentConfig).Assembly;
        foreach (var icon in IconResources)
        {
            using var stream = assembly.GetManifestResourceStream(icon);
            Assert.NotNull(stream);
            Assert.True(stream!.Length > 0);
            Span<byte> header = stackalloc byte[6];
            Assert.Equal(6, stream.Read(header));
            Assert.Equal(0, BitConverter.ToUInt16(header[..2]));
            Assert.Equal(1, BitConverter.ToUInt16(header[2..4]));
            Assert.Equal(6, BitConverter.ToUInt16(header[4..6]));
        }
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GSPTaskMiningAgent.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
