using GSPTaskMiningAnalyzer.Models;

namespace GSPTaskMiningAnalyzer;

public sealed class DataQualityService
{
    public DataQualitySummary Evaluate(AnalysisResult r)
    {
        var observed = Math.Max(0, (r.To - r.From)?.TotalSeconds ?? (r.ActiveSeconds + r.IdleSeconds + r.LockedSeconds + r.UnknownSeconds));
        var days = r.Events.Select(e => e.TimestampLocal.Date).Distinct().Count();
        var users = r.Events.Select(e => e.UserName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var machines = r.Events.Select(e => e.MachineName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var restarts = r.Events.Count(e => e.EventType.Equals("agent_start", StringComparison.OrdinalIgnoreCase));
        var unknownShare = observed > 0 ? r.UnknownSeconds / observed : 0;
        var coverage = days > 0 ? observed / (days * 8d * 3600d) : 0;
        var hasParallel = r.Events.GroupBy(e => new { e.UserName, e.MachineName, Second = e.TimestampUtc.ToUnixTimeSeconds() }).Any(g => g.Count(e => e.EventType == "agent_start") > 1);
        var level = observed < 2 * 3600 ? "red" : (observed < 16 * 3600 || days < 2 ? "yellow" : "green");
        var warning = level == "red"
            ? "Объём данных недостаточен для выводов. Показана только техническая статистика. Не используйте раздел автоматизации для принятия решений"
            : level == "yellow" ? "Объём данных ограничен: используйте выводы как предварительные" : "Объём данных достаточен для первичного анализа";
        return new DataQualitySummary(observed, days, users, machines, r.UniqueEvents, unknownShare, restarts, hasParallel, coverage, level, warning);
    }
}
