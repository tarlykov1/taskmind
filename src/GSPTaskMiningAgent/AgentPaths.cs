namespace GSPTaskMiningAgent;

public sealed class AgentPaths
{
    public AgentPaths(string root)
    {
        Root = root;
        Data = Path.Combine(root, "data");
        Logs = Path.Combine(Data, "logs");
        Screenshots = Path.Combine(Data, "screenshots");
        Archives = Path.Combine(Data, "archives");
        Errors = Path.Combine(Data, "errors");
        StatusFile = Path.Combine(Data, "agent-status.txt");
        StopFile = Path.Combine(Data, "stop.request");
        ConfigFile = Path.Combine(root, "config.json");
    }

    public string Root { get; }
    public string Data { get; }
    public string Logs { get; }
    public string Screenshots { get; }
    public string Archives { get; }
    public string Errors { get; }
    public string StatusFile { get; }
    public string StopFile { get; }
    public string ConfigFile { get; }

    public void EnsureAll()
    {
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Screenshots);
        Directory.CreateDirectory(Archives);
        Directory.CreateDirectory(Errors);
    }
}
