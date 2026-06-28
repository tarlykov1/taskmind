using GSPTaskMiningAnalyzer.Models;

namespace GSPTaskMiningAnalyzer;

public sealed class DurationCalculator
{
    public double MaxGapSeconds { get; init; } = 60;

    public IReadOnlyList<(LogEvent Event, double DurationSeconds)> Calculate(IReadOnlyList<LogEvent> events)
    {
        var ordered = events.OrderBy(e => e.MachineName).ThenBy(e => e.UserName).ThenBy(e => e.TimestampUtc).ToList();
        var result = new List<(LogEvent Event, double DurationSeconds)>();
        foreach (var group in ordered.GroupBy(e => new { e.MachineName, e.UserName }))
        {
            var items = group.OrderBy(e => e.TimestampUtc).ToList();
            for (var i = 0; i < items.Count; i++)
            {
                var currentEvent = items[i];
                var nextDelta = i + 1 < items.Count ? (items[i + 1].TimestampUtc - currentEvent.TimestampUtc).TotalSeconds : MaxGapSeconds;
                var candidates = new List<double> { MaxGapSeconds };
                if (currentEvent.DurationSeconds is > 0 and <= 86400) candidates.Add(currentEvent.DurationSeconds.Value);
                if (nextDelta >= 0) candidates.Add(nextDelta);
                result.Add((Event: currentEvent, DurationSeconds: Math.Max(0, candidates.Min())));
            }
        }
        return result.OrderBy(item => item.Event.TimestampUtc).ToList();
    }
}
