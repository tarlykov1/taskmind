using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using GSPTaskMiningAnalyzer.Models;

namespace GSPTaskMiningAnalyzer;

public sealed class HtmlReportService
{
    public string Write(AnalysisResult result, string outputDirectory) => CreateReport(result, outputDirectory);
    public string CreateReport(AnalysisResult result, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(result);
        Directory.CreateDirectory(outputDirectory);
        result.DataQuality ??= new DataQualityService().Evaluate(result);
        var data = BuildView(result);
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic) });
        var html = Template.Replace("__REPORT_JSON__", json).Replace("__GENERATED_AT__", "детерминированный отчёт");
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new InvalidOperationException("HtmlReportService returned empty HTML.");
        }
        var path = Path.Combine(outputDirectory, $"task_mining_dashboard_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        File.WriteAllText(path, html, new UTF8Encoding(false));
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            throw new IOException("HTML report file was not created or is empty: " + path);
        }
        return path;
    }

    private static object BuildView(AnalysisResult r)
    {
        var events = r.Events ?? [];
        var sessions = r.Sessions ?? [];
        var chains2 = r.Chains2 ?? [];
        var errors = r.Errors ?? [];
        var quality = r.DataQuality ?? new DataQualityService().Evaluate(r);
        var total = r.ActiveSeconds + r.IdleSeconds + r.LockedSeconds + r.UnknownSeconds;
        var intervals = BuildIntervals(sessions, TimeSpan.FromMinutes(15));
        var apps = sessions.Where(IsBusinessApp)
            .GroupBy(s => Safe(s.ProcessName, "unknown"), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var items = g.ToArray();
                var seconds = items.Sum(x => x.DurationSeconds);
                return new
                {
                    name = g.Key,
                    seconds,
                    share = total > 0 ? seconds / total : 0,
                    sessions = items.Length,
                    average = items.Length == 0 ? 0 : items.Average(x => x.DurationSeconds),
                    max = items.Length == 0 ? 0 : items.Max(x => x.DurationSeconds),
                    windows = items.Select(x => Safe(x.WindowTitle)).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                };
            })
            .OrderByDescending(x => x.seconds).Take(10).ToArray();
        var transitions = chains2.Where(x => x.Value >= 2 && GoodChain(x.Key ?? string.Empty)).OrderByDescending(x => x.Value).Take(10).Select(x => new { chain = x.Key, count = x.Value }).ToArray();
        var sessionRows = sessions.Select(s => new { windowTitle = Safe(s.WindowTitle), processName = Safe(s.ProcessName, "unknown"), machineName = Safe(s.MachineName), userName = Safe(s.UserName) }).ToArray();
        var eventRows = events.Select(e => new { windowTitle = Safe(e.WindowTitle), processName = Safe(e.ProcessName, "unknown"), machineName = Safe(e.MachineName), userName = Safe(e.UserName) }).ToArray();
        var insufficient = events.Count == 0 || sessions.Count == 0 || total <= 0;
        var warning = string.IsNullOrWhiteSpace(quality.Warning) ? (insufficient ? "Недостаточно данных" : string.Empty) : quality.Warning;
        return new { r.From, r.To, machine = string.Join(", ", events.Select(e => Safe(e.MachineName)).Where(x => x.Length > 0).Distinct()), user = string.Join(", ", events.Select(e => Safe(e.UserName)).Where(x => x.Length > 0).Distinct()), r.RowsRead, r.UniqueEvents, r.ActiveSeconds, r.IdleSeconds, r.LockedSeconds, r.UnknownSeconds, sessionCount = sessions.Count, switches = chains2.Values.Sum(), errors = errors.Count + r.BadRows, quality = quality with { Warning = warning }, structure = new[] { new { label = "Активность", seconds = r.ActiveSeconds }, new { label = "Простой", seconds = r.IdleSeconds }, new { label = "Блокировка", seconds = r.LockedSeconds }, new { label = "Неизвестно", seconds = r.UnknownSeconds } }, intervals, apps, transitions, sessions = sessionRows, events = eventRows };
    }

    private static bool IsBusinessApp(Session s) => s.State == "active" && !new[] { "GSPTaskMiningAgent", "GSPTaskMiningAnalyzer", "Taskmgr", "LockApp", "unknown", "" }.Contains(Safe(s.ProcessName), StringComparer.OrdinalIgnoreCase);
    private static string Safe(string? value, string fallback = "") => string.IsNullOrWhiteSpace(value) ? fallback : value;
    private static bool GoodChain(string c) { var parts = c.Split('→', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries); return parts.Length > 1 && parts.Zip(parts.Skip(1)).All(p => !p.First.Equals(p.Second, StringComparison.OrdinalIgnoreCase)) && !parts.Any(p => new[] { "GSPTaskMiningAgent", "GSPTaskMiningAnalyzer", "Taskmgr", "LockApp", "unknown" }.Contains(p, StringComparer.OrdinalIgnoreCase)); }
    private static object[] BuildIntervals(IReadOnlyCollection<Session>? sessions, TimeSpan step)
    {
        if (sessions is null || sessions.Count == 0) return [];

        var firstSessionStart = sessions.Min(s => s.Start).ToLocalTime();
        var start = new DateTimeOffset(
            firstSessionStart.Year,
            firstSessionStart.Month,
            firstSessionStart.Day,
            0,
            0,
            0,
            firstSessionStart.Offset);
        var end = sessions.Max(s => s.End).ToLocalTime();
        var rows = new List<object>();

        for (var intervalStart = start; intervalStart < end; intervalStart = intervalStart.Add(step))
        {
            var intervalEnd = intervalStart.Add(step);
            double active = 0;
            double idle = 0;
            double locked = 0;
            double unknown = 0;
            var switches = 0;

            foreach (var session in sessions)
            {
                var sessionStart = session.Start.ToLocalTime();
                var sessionEnd = session.End.ToLocalTime();
                var overlapStart = sessionStart > intervalStart ? sessionStart : intervalStart;
                var overlapEnd = sessionEnd < intervalEnd ? sessionEnd : intervalEnd;
                var overlapSeconds = Math.Max(0, (overlapEnd - overlapStart).TotalSeconds);

                if (overlapSeconds <= 0)
                {
                    continue;
                }

                switch (session.State)
                {
                    case "idle":
                        idle += overlapSeconds;
                        break;
                    case "locked":
                        locked += overlapSeconds;
                        break;
                    case "unknown":
                        unknown += overlapSeconds;
                        break;
                    default:
                        active += overlapSeconds;
                        break;
                }

                if (sessionStart >= intervalStart && sessionStart < intervalEnd)
                {
                    switches++;
                }
            }

            rows.Add(new { label = intervalStart.ToString("dd.MM HH:mm"), start = intervalStart, end = intervalEnd, active, idle, locked, unknown, switches = Math.Max(0, switches - 1) });
        }

        return rows.ToArray();
    }
    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    private const string Template = """
<!doctype html><html lang="ru"><head><meta charset="utf-8"><title>Task Mining – анализ рабочих процессов</title><style>body{font-family:Arial,sans-serif;margin:0;padding:22px;background:#f6f8fb;color:#172033}.card{background:#fff;border-radius:12px;padding:16px;margin:12px 0;box-shadow:0 1px 7px #d9dee8}.kpis{display:grid;grid-template-columns:repeat(4,1fr);gap:10px}.kpi{background:#eef4ff;border-radius:10px;padding:12px}.value{font-size:20px;font-weight:700}.warn{border-left:6px solid #dc2626;background:#fff1f2}svg{width:100%;height:auto}.legend span{margin-right:16px}.bar text{font-size:12px}table{width:100%;border-collapse:collapse}td,th{border-bottom:1px solid #e5e7eb;padding:8px;text-align:left}</style></head><body>
<h1>Task Mining – анализ рабочих процессов</h1><div class="card"><b>Период анализа:</b> <span id="period"></span><br><b>Компьютер:</b> <span id="machine"></span><br><b>Пользователь:</b> <span id="user"></span><br><b>Объём данных:</b> <span id="volume"></span></div><div id="warning" class="card warn"></div><div id="kpis" class="kpis"></div>
<div class="card"><h2>Диаграмма 1. Структура времени</h2><div class="legend">Активность · Простой · Блокировка · Неизвестно</div><svg id="structure" role="img"></svg></div>
<div class="card"><h2>Диаграмма 2. Лента активности</h2><p>Ось X: локальное время; Ось Y: секунды за 15 минут</p><svg id="timeline" role="img"></svg></div>
<div class="card"><h2>Диаграмма 3. Топ приложений</h2><p>Ось X: секунды; Ось Y: приложение</p><svg id="apps" role="img"></svg></div>
<div class="card"><h2>Диаграмма 4. Переключения</h2><p>Ось X: локальное время; Ось Y: фактические смены приложений</p><svg id="switches" role="img"></svg></div>
<div class="card"><h2>Диаграмма 5. Частые переходы</h2><table><thead><tr><th>Цепочка</th><th>Повторения</th></tr></thead><tbody id="transitions"></tbody></table></div>
<script id="report-data" type="application/json">__REPORT_JSON__</script><script>
const report=JSON.parse(document.getElementById('report-data').textContent);const fmt=s=>{s=Number(s||0);return s>=60?Math.floor(s/60)+' мин '+Math.round(s%60)+' сек':Math.round(s)+' сек'};period.textContent=(report.from||'')+' — '+(report.to||'');machine.textContent=report.machine||'—';user.textContent=report.user||'—';volume.textContent=fmt(report.quality.observedSeconds);warning.textContent=report.quality.warning;
[['Наблюдаемое время',fmt(report.quality.observedSeconds)],['Активное время',fmt(report.activeSeconds)],['Простой',fmt(report.idleSeconds)],['Компьютер заблокирован',fmt(report.lockedSeconds)],['Неизвестное время',fmt(report.unknownSeconds)],['Количество сессий',report.sessionCount],['Переключения',report.switches],['Ошибки сбора',report.errors]].forEach(x=>{let d=document.createElement('div');d.className='kpi';d.innerHTML='<div>'+x[0]+'</div><div class=value>'+x[1]+'</div>';kpis.appendChild(d)});
function empty(svg,msg){svg.innerHTML='<text x="20" y="40">'+msg+'</text>'} function rect(svg,x,y,w,h,c,t){let r=document.createElementNS('http://www.w3.org/2000/svg','rect');r.setAttribute('x',x);r.setAttribute('y',y);r.setAttribute('width',Math.max(0,w));r.setAttribute('height',h);r.setAttribute('fill',c);let tt=document.createElementNS('http://www.w3.org/2000/svg','title');tt.textContent=t;r.appendChild(tt);svg.appendChild(r)} function txt(svg,x,y,s){let e=document.createElementNS('http://www.w3.org/2000/svg','text');e.setAttribute('x',x);e.setAttribute('y',y);e.textContent=s;svg.appendChild(e)}
(function(){let svg=structure,total=report.structure.reduce((a,b)=>a+b.seconds,0),x=0,colors=['#16a34a','#f59e0b','#64748b','#94a3b8'];svg.setAttribute('viewBox','0 0 900 100');if(total<=0)return empty(svg,'Недостаточно данных');report.structure.forEach((p,i)=>{let w=p.seconds/total*760;rect(svg,120+x,25,w,26,colors[i],p.label+': '+fmt(p.seconds));txt(svg,120+x,70,p.label+' '+fmt(p.seconds)+' ('+(p.seconds*100/total).toFixed(1)+'%)');x+=w})})();
function lines(svg,rows,fields){svg.setAttribute('viewBox','0 0 900 220');if(rows.length<2)return empty(svg,'Недостаточно данных');let max=Math.max(1,...rows.flatMap(r=>fields.map(f=>r[f]))), colors=['#16a34a','#f59e0b','#64748b'];fields.forEach((f,ci)=>{let pts=rows.map((r,i)=>[60+i*(800/(rows.length-1)),180-r[f]/max*140]);let p=document.createElementNS('http://www.w3.org/2000/svg','polyline');p.setAttribute('fill','none');p.setAttribute('stroke',colors[ci]);p.setAttribute('points',pts.map(a=>a.join(',')).join(' '));svg.appendChild(p)});txt(svg,55,205,rows[0].label);txt(svg,780,205,rows[rows.length-1].label);txt(svg,10,25,'секунды')}
lines(timeline,report.intervals,['active','idle','locked']);lines(switches,report.intervals,['switches']);
(function(){let svg=apps,rows=report.apps;svg.setAttribute('viewBox','0 0 900 '+Math.max(80,rows.length*34+40));if(rows.length<1)return empty(svg,'Недостаточно данных');let max=Math.max(...rows.map(r=>r.seconds));rows.forEach((r,i)=>{let y=30+i*34;txt(svg,10,y+16,r.name);rect(svg,180,y,r.seconds/max*560,22,'#2563eb',r.name+': '+fmt(r.seconds)+', '+(r.share*100).toFixed(1)+'%, сессий '+r.sessions+', средняя '+fmt(r.average));txt(svg,750,y+16,fmt(r.seconds)+' · '+r.sessions+' сесс.')})})();
(report.transitions||[]).forEach(t=>{let tr=document.createElement('tr');tr.innerHTML='<td>'+t.chain+'</td><td>'+t.count+'</td>';transitions.appendChild(tr)});if(!transitions.children.length){let tr=document.createElement('tr');tr.innerHTML='<td colspan=2>Недостаточно данных</td>';transitions.appendChild(tr)}
</script></body></html>
""";
}
