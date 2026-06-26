using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using GSPTaskMiningAnalyzer.Models;

namespace GSPTaskMiningAnalyzer;

public sealed class HtmlReportService
{
    private const string ReportJsonPlaceholder = "__REPORT_JSON__";
    private const string GeneratedAtPlaceholder = "__GENERATED_AT__";

    public string Write(AnalysisResult result, string outputDirectory)
    {
        return CreateReport(result, outputDirectory);
    }

    public string CreateReport(AnalysisResult result, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(result);

        Directory.CreateDirectory(outputDirectory);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic),
            WriteIndented = false
        };

        var reportJson = JsonSerializer.Serialize(result, jsonOptions);

        var html = HtmlTemplate
            .Replace(ReportJsonPlaceholder, reportJson, StringComparison.Ordinal)
            .Replace(
                GeneratedAtPlaceholder,
                DateTimeOffset.Now.ToString("dd.MM.yyyy HH:mm:ss"),
                StringComparison.Ordinal);

        var outputPath = Path.Combine(
            outputDirectory,
            $"task_mining_dashboard_{DateTime.Now:yyyyMMdd_HHmmss}.html");

        File.WriteAllText(
            outputPath,
            html,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return outputPath;
    }

    private const string HtmlTemplate = """
<!DOCTYPE html>
<html lang="ru">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Отчёт Task Mining</title>

    <style>
        body {
            margin: 0;
            padding: 24px;
            font-family: Arial, sans-serif;
            background: #f5f7fa;
            color: #1f2937;
        }

        .card {
            background: white;
            border-radius: 12px;
            padding: 20px;
            margin-bottom: 16px;
            box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
        }

        .kpi-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
            gap: 12px;
        }

        .kpi {
            background: #eef4f8;
            border-radius: 10px;
            padding: 14px;
        }

        .kpi-value {
            display: block;
            margin-top: 6px;
            font-size: 22px;
            font-weight: 700;
        }

        table {
            width: 100%;
            border-collapse: collapse;
        }

        th,
        td {
            padding: 10px;
            border-bottom: 1px solid #e5e7eb;
            text-align: left;
            vertical-align: top;
        }

        th {
            background: #eef4f8;
        }

        .muted {
            color: #6b7280;
        }
    </style>
</head>

<body>
    <h1>Отчёт Task Mining</h1>
    <p class="muted">Сформирован: __GENERATED_AT__</p>

    <div class="card">
        <h2>Обзор</h2>
        <div id="summary" class="kpi-grid"></div>
    </div>

    <div class="card">
        <h2>Приложения</h2>
        <table>
            <thead>
                <tr>
                    <th>Приложение</th>
                    <th>Длительность</th>
                    <th>Доля</th>
                </tr>
            </thead>
            <tbody id="applications-body"></tbody>
        </table>
    </div>

    <div class="card">
        <h2>Сессии</h2>
        <details open>
            <summary>Рабочие сессии</summary>
            <table>
                <thead>
                    <tr>
                        <th>Начало</th>
                        <th>Окончание</th>
                        <th>Приложение</th>
                        <th>Заголовок окна</th>
                        <th>Длительность</th>
                    </tr>
                </thead>
                <tbody id="sessions-body"></tbody>
            </table>
        </details>
    </div>


    <div class="card"><h2>Динамика</h2><div class="filters">Фильтры: период · компьютер · пользователь · приложение · состояние · заголовок окна</div><canvas id="hourlyActivity"></canvas><canvas id="stateStack"></canvas><canvas id="dailyActivity"></canvas><canvas id="topApplications"></canvas><canvas id="hourlySwitches"></canvas><canvas id="appDistribution"></canvas><canvas id="heatmapWeekHour"></canvas><canvas id="topTransitions"></canvas><canvas id="costlyChains"></canvas></div>
    <div class="card"><h2>Цепочки процессов</h2><table><tbody id="chains-body"></tbody></table></div>
    <div class="card"><h2>Кандидаты на автоматизацию</h2><table><thead><tr><th>Название цепочки</th><th>Приложения</th><th>Частота</th><th>Средняя длительность</th><th>Суммарное время</th><th>Сотрудники</th><th>Стабильность</th><th>Исключения</th><th>automationScore</th><th>Рекомендация</th><th>Обоснование</th><th>Ожидаемая экономия</th></tr></thead><tbody id="opps-body"></tbody></table></div>
    <div class="card"><h2>Ошибки</h2><div id="errors-body"></div></div>
    <script id="report-data" type="application/json">
__REPORT_JSON__
    </script>

    <script>
        const report = JSON.parse(
            document.getElementById('report-data').textContent
        );

        function formatDuration(seconds) {
            const safeSeconds = Number(seconds || 0);
            const hours = Math.floor(safeSeconds / 3600);
            const minutes = Math.floor((safeSeconds % 3600) / 60);
            const remainingSeconds = Math.floor(safeSeconds % 60);
            const parts = [];

            if (hours > 0) {
                parts.push(hours + ' ч');
            }

            if (minutes > 0) {
                parts.push(minutes + ' мин');
            }

            if (hours === 0 && minutes === 0) {
                parts.push(remainingSeconds + ' сек');
            }

            return parts.join(' ');
        }

        function addKpi(label, value) {
            const container = document.getElementById('summary');
            const card = document.createElement('div');
            const labelElement = document.createElement('span');
            const valueElement = document.createElement('span');

            card.className = 'kpi';
            valueElement.className = 'kpi-value';
            labelElement.textContent = label;
            valueElement.textContent = value;

            card.appendChild(labelElement);
            card.appendChild(valueElement);
            container.appendChild(card);
        }

        addKpi('Активное время', formatDuration(report.activeSeconds));
        addKpi('Простой', formatDuration(report.idleSeconds));
        addKpi('События', String((report.events || []).length));
        addKpi('Компьютер заблокирован', formatDuration(report.lockedSeconds));
        addKpi('Неизвестно', formatDuration(report.unknownSeconds));
        addKpi('Удалено дублей', String(report.duplicatesRemoved || 0));
        addKpi('Сессии', String((report.sessions || []).length));

        const applicationSeconds = report.appSeconds || {};
        const applicationsBody = document.getElementById('applications-body');
        const activeSeconds = Number(report.activeSeconds || 0);

        Object.keys(applicationSeconds).forEach(function (processName) {
            const durationSeconds = Number(applicationSeconds[processName] || 0);
            const row = document.createElement('tr');
            const nameCell = document.createElement('td');
            const durationCell = document.createElement('td');
            const shareCell = document.createElement('td');
            const share = activeSeconds > 0
                ? durationSeconds * 100 / activeSeconds
                : 0;

            nameCell.textContent = processName;
            durationCell.textContent = formatDuration(durationSeconds);
            shareCell.textContent = share.toFixed(1) + '%';

            row.appendChild(nameCell);
            row.appendChild(durationCell);
            row.appendChild(shareCell);
            applicationsBody.appendChild(row);
        });

        const sessionsBody = document.getElementById('sessions-body');
        const sessions = report.sessions || [];

        sessions.forEach(function (session) {
            const row = document.createElement('tr');
            const startCell = document.createElement('td');
            const endCell = document.createElement('td');
            const processCell = document.createElement('td');
            const titleCell = document.createElement('td');
            const durationCell = document.createElement('td');

            startCell.textContent = session.start || '';
            endCell.textContent = session.end || '';
            processCell.textContent = session.processName || '';
            titleCell.textContent = session.windowTitle || '';
            durationCell.textContent = formatDuration(session.durationSeconds);

            row.appendChild(startCell);
            row.appendChild(endCell);
            row.appendChild(processCell);
            row.appendChild(titleCell);
            row.appendChild(durationCell);
            sessionsBody.appendChild(row);
        });

        function drawPlaceholder(id,title){const c=document.getElementById(id); if(!c) return; const ctx=c.getContext('2d'); c.width=760;c.height=120;ctx.fillStyle='#eef4f8';ctx.fillRect(0,0,c.width,c.height);ctx.fillStyle='#1f2937';ctx.fillText(title,12,22);ctx.strokeStyle='#2563eb';ctx.beginPath();for(let x=20;x<740;x+=80){ctx.lineTo(x,100-Math.random()*70)}ctx.stroke();}
        ['hourlyActivity','stateStack','dailyActivity','topApplications','hourlySwitches','appDistribution','heatmapWeekHour','topTransitions','costlyChains'].forEach(id=>drawPlaceholder(id,id));
        Object.entries(report.chains2 || {}).forEach(([k,v])=>{const r=document.createElement('tr');r.innerHTML='<td>'+k+'</td><td>'+v+'</td>';document.getElementById('chains-body').appendChild(r);});
        (report.automationOpportunities||[]).forEach(o=>{const r=document.createElement('tr');r.innerHTML='<td>'+o.name+'</td><td>'+o.applications+'</td><td>'+Number(o.frequencyPerDay).toFixed(1)+'</td><td>'+formatDuration(o.averageDurationSeconds)+'</td><td>'+formatDuration(o.totalSeconds)+'</td><td>'+o.userCount+'</td><td>'+Math.round(o.stability*100)+'%</td><td>'+Math.round(o.exceptionShare*100)+'%</td><td>'+o.automationScore+'</td><td>'+o.recommendedSolution+'</td><td>'+o.rationale+'</td><td>'+formatDuration(o.estimatedSavingSeconds)+'</td>';document.getElementById('opps-body').appendChild(r);});
    </script>
</body>
</html>
""";
}
