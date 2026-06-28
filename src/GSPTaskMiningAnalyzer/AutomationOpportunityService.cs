using GSPTaskMiningAnalyzer.Models;

namespace GSPTaskMiningAnalyzer;

public interface IInsightProvider { IReadOnlyList<AutomationOpportunity> GetInsights(AnalysisResult result); }
public sealed class RuleBasedInsightProvider : IInsightProvider { public IReadOnlyList<AutomationOpportunity> GetInsights(AnalysisResult r) => new AutomationOpportunityService().Find(r); }

public sealed class AutomationOpportunityService
{
    public int MinimumRepeats { get; init; } = 5;
    public int MinimumDays { get; init; } = 2;
    public double MinimumStability { get; init; } = 0.15;

    public List<AutomationOpportunity> Find(AnalysisResult r)
    {
        if (r.DataQuality is not null && r.DataQuality.Level == "red") return [];
        var list = new List<AutomationOpportunity>();
        foreach (var c in r.Chains2.Concat(r.Chains3).GroupBy(x => x.Key).Select(g => new { Key = g.Key, Count = g.Sum(x => x.Value) }).OrderByDescending(x => x.Count).Take(20))
        {
            var apps = c.Key.Split('→', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (apps.Length < 2) continue;
            var sessions = r.Sessions.Where(s => apps.Contains(s.ProcessName, StringComparer.OrdinalIgnoreCase)).ToList();
            var days = sessions.Select(s => s.Start.LocalDateTime.Date).Distinct().Count();
            var users = sessions.Select(s => s.UserName).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var stability = Math.Min(1.0, c.Count / Math.Max(1.0, r.Sessions.Count));
            if (c.Count < MinimumRepeats || days < MinimumDays || users < 2 || stability < MinimumStability) continue;
            var total = sessions.Sum(s => s.DurationSeconds);
            var avg = c.Count > 0 ? total / c.Count : 0;
            var freq = days > 0 ? c.Count / (double)days : 0;
            var score = (int)Math.Clamp(c.Count * 4 + Math.Min(25, avg / 60 * 5) + stability * 35 + Math.Min(20, users * 5), 0, 100);
            var solution = DetermineSolution(apps, c.Key);
            list.Add(new(c.Key, string.Join(", ", apps), freq, avg, total, users, sessions.Select(s => s.MachineName).Distinct().Count(), stability, 0, total * .2, Math.Min(100, score + 10), score, "доказанная повторяемость", solution, $"Цепочка встречается {c.Count} раз за {days} дн.; гипотеза требует интервью и проверки."));
        }
        return list;
    }

    private static string DetermineSolution(string[] apps, string chain)
    {
        var text = (chain + " " + string.Join(' ', apps)).ToLowerInvariant();
        if (text.Contains("excel") || text.Contains("word") || text.Contains("notepad")) return "RPA";
        if (text.Contains("explorer") || text.Contains("cmd") || text.Contains("powershell")) return "Скрипт/RPA";
        if (text.Contains("mail") || text.Contains("classification") || text.Contains("классиф")) return "ИИ";
        if (apps.Length >= 3) return "Изменение бизнес-процесса";
        return "Требуется наблюдение и интервью";
    }
}
