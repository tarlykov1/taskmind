using GSPTaskMiningAnalyzer.Models;

namespace GSPTaskMiningAnalyzer;

public sealed class DurationCalculator
{
    public double MaxGapSeconds { get; init; } = 60;

    public List<(LogEvent Event, double Seconds)> Calculate(IReadOnlyList<LogEvent> events)
    {
        var ordered = events.OrderBy(e => e.MachineName).ThenBy(e => e.UserName).ThenBy(e => e.TimestampUtc).ToList();
        var result = new List<(LogEvent, double)>();
        foreach (var group in ordered.GroupBy(e => new { e.MachineName, e.UserName }))
        {
            var items = group.OrderBy(e => e.TimestampUtc).ToList();
            for (var i = 0; i < items.Count; i++)
            {
                var e = items[i];
                var nextDelta = i + 1 < items.Count ? (items[i + 1].TimestampUtc - e.TimestampUtc).TotalSeconds : MaxGapSeconds;
                var candidates = new List<double> { MaxGapSeconds };
                if (e.DurationSeconds is > 0 and <= 86400) candidates.Add(e.DurationSeconds.Value);
                if (nextDelta >= 0) candidates.Add(nextDelta);
                result.Add((e, Math.Max(0, candidates.Min())));
            }
        }
        return result.OrderBy(x => x.Event.TimestampUtc).ToList();
    }
}
