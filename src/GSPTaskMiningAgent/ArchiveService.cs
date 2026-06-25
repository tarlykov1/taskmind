using System.IO.Compression;

namespace GSPTaskMiningAgent;

public static class ArchiveService
{
    public static void Run(AgentPaths paths, AgentConfig config, DateTimeOffset nowUtc)
    {
        ArchiveOldFiles(paths.Logs, paths.Archives, "logs", "*.jsonl", config.ArchiveAfterDays, nowUtc);
        ArchiveOldFiles(paths.Screenshots, paths.Archives, "screenshots", "*.png", config.ArchiveAfterDays, nowUtc);
        DeleteOldArchives(paths.Archives, config.RetainArchivesDays, nowUtc);
    }

    private static void ArchiveOldFiles(string sourceDir, string archiveDir, string label, string pattern, int ageDays, DateTimeOffset nowUtc)
    {
        foreach (var file in Directory.EnumerateFiles(sourceDir, pattern))
        {
            var info = new FileInfo(file);
            if (nowUtc - info.LastWriteTimeUtc < TimeSpan.FromDays(Math.Max(1, ageDays))) continue;
            var zipName = Path.Combine(archiveDir, $"{label}-{nowUtc:yyyyMMdd}.zip");
            using var zip = ZipFile.Open(zipName, ZipArchiveMode.Update);
            zip.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Optimal);
            File.Delete(file);
        }
    }

    private static void DeleteOldArchives(string archiveDir, int retainDays, DateTimeOffset nowUtc)
    {
        foreach (var file in Directory.EnumerateFiles(archiveDir, "*.zip"))
        {
            var info = new FileInfo(file);
            if (nowUtc - info.LastWriteTimeUtc > TimeSpan.FromDays(Math.Max(1, retainDays))) File.Delete(file);
        }
    }
}
