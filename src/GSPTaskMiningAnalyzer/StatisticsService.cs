using GSPTaskMiningAnalyzer.Models; namespace GSPTaskMiningAnalyzer; public sealed class StatisticsService { static readonly HashSet<string> ServiceEvents=new(StringComparer.OrdinalIgnoreCase){"agent_start","agent_stop"}; static readonly HashSet<string> ExcludedTech=new(StringComparer.OrdinalIgnoreCase){"LockApp","LogonUI","Taskmgr","GSPTaskMiningAgent","GSPTaskMiningAnalyzer"}; public bool ExcludeUnknown{get;set;}=true; public bool ExcludeTechnicalProcesses{get;set;}=true; public AnalysisResult Analyze(IEnumerable<LogEvent> input,double maxGap=60){var raw=input.OrderBy(e=>e.TimestampUtc).ToList(); var events=LogReader.Dedup(raw).OrderBy(e=>e.TimestampUtc).ToList(); var res=new AnalysisResult{RowsRead=raw.FirstOrDefault()?.ReadRows ?? raw.Count,UniqueEvents=events.Count,DuplicatesRemoved=(raw.FirstOrDefault()?.DuplicatesRemoved??(raw.Count-events.Count)),BadRows=raw.FirstOrDefault()?.BadRows??0}; res.Events.AddRange(events); res.From=events.FirstOrDefault()?.TimestampUtc; res.To=events.LastOrDefault()?.TimestampUtc; var work=events.Where(e=>!ServiceEvents.Contains(e.EventType)).ToList(); var d=new DurationCalculator{MaxGapSeconds=maxGap}.Calculate(work); foreach(var item in d){var e=item.Event; var s=item.DurationSeconds;var state=State(e); if(state=="idle") {res.IdleSeconds+=s; Add(res.IdleByDay,e.TimestampLocal.ToString("yyyy-MM-dd"),s); Add(res.IdleByHour,e.TimestampLocal.Hour,s);} else if(state=="locked") {res.LockedSeconds+=s; Add(res.LockedByDay,e.TimestampLocal.ToString("yyyy-MM-dd"),s); Add(res.LockedByHour,e.TimestampLocal.Hour,s);} else if(state=="unknown") {res.UnknownSeconds+=s; Add(res.UnknownByDay,e.TimestampLocal.ToString("yyyy-MM-dd"),s);} else {res.ActiveSeconds+=s; Add(res.AppSeconds,string.IsNullOrEmpty(e.ProcessName)?"unknown":e.ProcessName,s); Add(res.ActiveByDay,e.TimestampLocal.ToString("yyyy-MM-dd"),s); Add(res.ActiveByHour,e.TimestampLocal.Hour,s);} }
 BuildSessions(res,d); for(int i=0;i<res.Sessions.Count-1;i++){var a=res.Sessions[i];var b=res.Sessions[i+1]; if(!Chainable(a)||!Chainable(b)||string.Equals(a.ProcessName,b.ProcessName,StringComparison.OrdinalIgnoreCase)) continue; var k=$"{a.ProcessName} → {b.ProcessName}"; res.Chains2[k]=res.Chains2.GetValueOrDefault(k)+1; res.SwitchesByHour[b.Start.ToLocalTime().Hour]=res.SwitchesByHour.GetValueOrDefault(b.Start.ToLocalTime().Hour)+1;} for(int i=0;i<res.Sessions.Count-2;i++){var a=res.Sessions[i];var b=res.Sessions[i+1];var c=res.Sessions[i+2]; if(!Chainable(a)||!Chainable(b)||!Chainable(c)) continue; if(a.ProcessName==b.ProcessName&&b.ProcessName==c.ProcessName) continue; res.Chains3[$"{a.ProcessName} → {b.ProcessName} → {c.ProcessName}"]=res.Chains3.GetValueOrDefault($"{a.ProcessName} → {b.ProcessName} → {c.ProcessName}")+1;} var dataQuality=new DataQualityService().Evaluate(res); res.DataQuality=dataQuality; res.AutomationOpportunities.AddRange(new AutomationOpportunityService().Find(res)); return res;} string State(LogEvent e){if(e.ProcessName.Equals("LockApp",StringComparison.OrdinalIgnoreCase)||e.ProcessName.Equals("LogonUI",StringComparison.OrdinalIgnoreCase)) return "locked"; if(e.IsIdle||e.EventType=="idle_start") return "idle"; if(string.IsNullOrWhiteSpace(e.ProcessName)||e.ProcessName.Equals("unknown",StringComparison.OrdinalIgnoreCase)) return "unknown"; return "active";} bool Chainable(Session s){if(s.State!="active"||string.IsNullOrWhiteSpace(s.ProcessName))return false; if(ExcludeUnknown&&s.ProcessName.Equals("unknown",StringComparison.OrdinalIgnoreCase))return false; if(ExcludeTechnicalProcesses&&ExcludedTech.Contains(s.ProcessName))return false; return true;} static void Add<TKey>(Dictionary<TKey,double> d,TKey k,double v) where TKey:notnull=>d[k]=d.GetValueOrDefault(k)+v; void BuildSessions(AnalysisResult r,IReadOnlyList<(LogEvent Event,double DurationSeconds)> d)
{
    foreach (var group in d.GroupBy(x => new { x.Event.MachineName, x.Event.UserName }))
    {
        Session? cur = null;
        DateTimeOffset? lastEnd = null;
        foreach (var item in group.OrderBy(x => x.Event.TimestampUtc))
        {
            var e = item.Event;
            var seconds = item.DurationSeconds;
            var start = lastEnd.HasValue && e.TimestampUtc < lastEnd.Value ? lastEnd.Value : e.TimestampUtc;
            var end = start.AddSeconds(Math.Max(0, seconds));
            if (end < start) end = start;
            var state = State(e);
            var shot = string.IsNullOrEmpty(e.ScreenshotFile) ? 0 : 1;
            if (cur is null
                || !cur.ProcessName.Equals(e.ProcessName, StringComparison.Ordinal)
                || !cur.WindowTitle.Equals(e.WindowTitle, StringComparison.Ordinal)
                || !cur.State.Equals(state, StringComparison.Ordinal)
                || start > cur.End)
            {
                if (cur is not null) r.Sessions.Add(cur);
                cur = new(start, end, Math.Max(0, (end - start).TotalSeconds), e.ProcessName, e.WindowTitle, e.MachineName, e.UserName, shot, state);
            }
            else
            {
                var mergedEnd = end > cur.End ? end : cur.End;
                cur = cur with { End = mergedEnd, DurationSeconds = Math.Max(0, (mergedEnd - cur.Start).TotalSeconds), ScreenshotCount = cur.ScreenshotCount + shot };
            }
            lastEnd = end;
        }
        if (cur is not null) r.Sessions.Add(cur);
    }
    r.Sessions.Sort((a,b)=>a.Start.CompareTo(b.Start));
}
}
