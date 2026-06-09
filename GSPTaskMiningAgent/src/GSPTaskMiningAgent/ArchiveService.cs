using System.IO.Compression;
using GSPTaskMiningAgent.Models;

namespace GSPTaskMiningAgent;

public sealed class ArchiveService
{
    private readonly AppConfig _config;
    private readonly EventLogger _logger;

    public ArchiveService(AppConfig config, EventLogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public void ArchivePreviousDays()
    {
        if (!_config.Agent.ArchiveDaily)
        {
            return;
        }

        var logsDir = Path.Combine(_config.Agent.LogRoot, "logs");
        if (!Directory.Exists(logsDir))
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var dates = Directory.EnumerateFiles(logsDir, "*_events.jsonl")
            .Select(path => Path.GetFileName(path)[..10])
            .Where(date => DateOnly.TryParse(date, out var parsed) && parsed < today)
            .Distinct()
            .ToArray();

        foreach (var date in dates)
        {
            try
            {
                ArchiveDay(date);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to archive {date}");
            }
        }
    }

    private void ArchiveDay(string date)
    {
        var archiveDir = Path.Combine(_config.Agent.LogRoot, "archives");
        Directory.CreateDirectory(archiveDir);
        var archivePath = Path.Combine(archiveDir, $"{date}_{Environment.MachineName}.zip");
        if (File.Exists(archivePath))
        {
            return;
        }

        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            AddIfExists(archive, Path.Combine(_config.Agent.LogRoot, "logs", $"{date}_events.jsonl"), $"logs/{date}_events.jsonl");
            AddIfExists(archive, Path.Combine(_config.Agent.LogRoot, "logs", $"{date}_windows.csv"), $"logs/{date}_windows.csv");
            AddIfExists(archive, Path.Combine(_config.Agent.LogRoot, "errors", "agent_errors.log"), "errors/agent_errors.log");

            var screenshotDir = Path.Combine(_config.Agent.LogRoot, "screenshots", date);
            if (Directory.Exists(screenshotDir))
            {
                foreach (var file in Directory.EnumerateFiles(screenshotDir, "*.*", SearchOption.AllDirectories))
                {
                    archive.CreateEntryFromFile(file, Path.Combine("screenshots", date, Path.GetFileName(file)));
                }
            }
        }

        _logger.Log(new ActivityEvent
        {
            Timestamp = DateTimeOffset.Now,
            User = Environment.UserName,
            Machine = Environment.MachineName,
            EventType = "archive_created",
            Message = archivePath
        });

        if (_config.Agent.CopyArchiveToNetworkShare)
        {
            CopyToNetworkShare(archivePath);
        }
    }

    private void CopyToNetworkShare(string archivePath)
    {
        try
        {
            var targetDir = Path.Combine(_config.Agent.NetworkSharePath, Environment.MachineName);
            Directory.CreateDirectory(targetDir);
            File.Copy(archivePath, Path.Combine(targetDir, Path.GetFileName(archivePath)), overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy archive to network share");
        }
    }

    private static void AddIfExists(ZipArchive archive, string sourcePath, string entryName)
    {
        if (File.Exists(sourcePath))
        {
            archive.CreateEntryFromFile(sourcePath, entryName);
        }
    }
}
