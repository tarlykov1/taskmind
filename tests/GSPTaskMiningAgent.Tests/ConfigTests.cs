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
        Assert.Equal(WindowTitleMode.Plain, WindowTitlePrivacy.Resolve(config));
        Assert.True(config.HashUserName);
    }

    [Fact]
    public void WindowTitleModesWork()
    {
        Assert.Equal("Secret", WindowTitlePrivacy.Apply("Secret", "app", new AgentConfig { Privacy = new PrivacyConfig { WindowTitleMode = "plain" } }));
        Assert.StartsWith("masked:", WindowTitlePrivacy.Apply("Secret", "app", new AgentConfig { Privacy = new PrivacyConfig { WindowTitleMode = "masked" } }));
        Assert.Equal("", WindowTitlePrivacy.Apply("Secret", "app", new AgentConfig { Privacy = new PrivacyConfig { WindowTitleMode = "off" } }));
        Assert.Equal("", WindowTitlePrivacy.Apply("Secret", "app", new AgentConfig { ExcludedProcesses = new[] { "app" } }));
        Assert.Equal(WindowTitleMode.Off, WindowTitlePrivacy.Resolve(new AgentConfig { Privacy = null, CaptureWindowTitle = false }));
        Assert.Equal(WindowTitleMode.Masked, WindowTitlePrivacy.Resolve(new AgentConfig { Privacy = null, CaptureWindowTitle = true, MaskWindowTitle = true }));
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
