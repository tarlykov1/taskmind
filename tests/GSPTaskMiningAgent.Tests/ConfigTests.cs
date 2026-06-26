using Xunit;
using GSPTaskMiningAgent;

namespace GSPTaskMiningAgent.Tests;

public sealed class ConfigTests
{
    [Fact]
    public void LoadOrCreateWritesDefaultConfig()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "config.json");
        var config = AgentConfig.LoadOrCreate(path);
        Assert.True(File.Exists(path));
        Assert.True(config.MaskWindowTitles);
        Assert.True(config.HashUserName);
    }

    [Fact]
    public void PathsCreateRequiredDirectories()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var paths = new AgentPaths(dir);
        paths.EnsureAll();
        Assert.True(Directory.Exists(paths.Logs));
        Assert.True(Directory.Exists(paths.Screenshots));
        Assert.True(Directory.Exists(paths.Archives));
        Assert.True(Directory.Exists(paths.Errors));
    }
}
