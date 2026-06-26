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
                DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
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
    <title>GSP Task Mining Report</title>

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
    <h1>GSP Task Mining Report</h1>
    <p class="muted">Generated: __GENERATED_AT__</p>

    <div class="card">
        <h2>Summary</h2>
        <div id="summary" class="kpi-grid"></div>
    </div>

    <div class="card">
        <h2>Applications</h2>
        <table>
            <thead>
                <tr>
                    <th>Application</th>
                    <th>Duration</th>
                    <th>Share</th>
                </tr>
            </thead>
            <tbody id="applications-body"></tbody>
        </table>
    </div>

    <div class="card">
        <h2>Sessions</h2>
        <details open>
            <summary>Timeline</summary>
            <table>
                <thead>
                    <tr>
                        <th>Start</th>
                        <th>End</th>
                        <th>Application</th>
                        <th>Window title</th>
                        <th>Duration</th>
                    </tr>
                </thead>
                <tbody id="sessions-body"></tbody>
            </table>
        </details>
    </div>

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
                parts.push(hours + ' h');
            }

            if (minutes > 0) {
                parts.push(minutes + ' min');
            }

            if (hours === 0 && minutes === 0) {
                parts.push(remainingSeconds + ' sec');
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

        addKpi('Active time', formatDuration(report.activeSeconds));
        addKpi('Idle time', formatDuration(report.idleSeconds));
        addKpi('Events', String((report.events || []).length));
        addKpi('Sessions', String((report.sessions || []).length));

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
    </script>
</body>
</html>
""";
}
