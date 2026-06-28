using System.IO.Compression;
using System.Text;
using GSPTaskMiningAnalyzer.Models;

namespace GSPTaskMiningAnalyzer;

public sealed class ExcelReportService
{
    static readonly string[] Sheets =
    [
        "Дашборд",
        "Динамика по интервалам",
        "Приложения",
        "Операции",
        "Сессии",
        "Цепочки процессов",
        "Кандидаты на автоматизацию",
        "Ошибки",
        "Исходные данные"
    ];

    public string Write(AnalysisResult r, string output, bool raw)
    {
        Directory.CreateDirectory(output);
        r.DataQuality ??= new DataQualityService().Evaluate(r);
        var names = raw ? Sheets : Sheets.Where(x => x != "Исходные данные").ToArray();
        var path = Path.Combine(output, $"task_mining_report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

        using var z = ZipFile.Open(path, ZipArchiveMode.Create);
        Add(
            z,
            "[Content_Types].xml",
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
                + string.Concat(
                    Enumerable
                        .Range(1, names.Length)
                        .Select(i => $"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"))
                + "<Override PartName=\"/xl/charts/chart1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.drawingml.chart+xml\"/></Types>");
        Add(
            z,
            "_rels/.rels",
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        Add(
            z,
            "xl/_rels/workbook.xml.rels",
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + string.Concat(
                    Enumerable
                        .Range(1, names.Length)
                        .Select(i => $"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>"))
                + "</Relationships>");
        Add(
            z,
            "xl/workbook.xml",
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>"
                + string.Concat(names.Select((n, i) => $"<sheet name=\"{Esc(n)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>"))
                + "</sheets></workbook>");

        for (var i = 0; i < names.Length; i++)
        {
            Add(z, $"xl/worksheets/sheet{i + 1}.xml", Sheet(names[i], r));
        }

        Add(z, "xl/charts/chart1.xml", ChartXml());
        return path;
    }

    static string Sheet(string name, AnalysisResult r)
    {
        List<string[]> rows = name switch
        {
            "Дашборд" => Dashboard(r),
            "Динамика по интервалам" => Intervals(r),
            "Приложения" => Apps(r),
            "Операции" => Ops(r),
            "Сессии" => Sessions(r),
            "Цепочки процессов" => Chains(r),
            "Кандидаты на автоматизацию" => Opps(r),
            "Исходные данные" => Raw(r),
            _ => Errors(r)
        };
        var width = Enumerable
            .Range(1, Math.Max(1, rows.Max(x => x.Length)))
            .Select(i => $"<col min=\"{i}\" max=\"{i}\" width=\"24\" customWidth=\"1\"/>");
        return $"<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews><cols>{string.Concat(width)}</cols><sheetData>{string.Concat(rows.Select((r, i) => $"<row r=\"{i + 1}\">{Cells(r)}</row>"))}</sheetData><autoFilter ref=\"A1:{Col(rows.Max(x => x.Length))}{rows.Count}\"/></worksheet>";
    }

    static List<string[]> Dashboard(AnalysisResult r) =>
    [
        ["Task Mining – анализ рабочих процессов", ""],
        ["Период", $"{r.From:O} — {r.To:O}"],
        ["Объём данных", r.DataQuality!.ObservedSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)],
        ["Предупреждение", r.DataQuality.Warning],
        ["Наблюдаемое время", r.DataQuality.ObservedSeconds.ToString()],
        ["Активное время", r.ActiveSeconds.ToString()],
        ["Простой", r.IdleSeconds.ToString()],
        ["Компьютер заблокирован", r.LockedSeconds.ToString()],
        ["Неизвестное время", r.UnknownSeconds.ToString()],
        ["Количество сессий", r.Sessions.Count.ToString()],
        ["Переключения", r.Chains2.Values.Sum().ToString()],
        ["Ошибки сбора", (r.Errors.Count + r.BadRows).ToString()]
    ];

    static List<string[]> Intervals(AnalysisResult r) =>
        new List<string[]>
        {
            new[]
            {
                "Дата",
                "Интервал начала",
                "Интервал окончания",
                "Активность, сек",
                "Простой, сек",
                "Блокировка, сек",
                "Неизвестно, сек",
                "Переключения"
            }
        }
            .Concat(
                r.Sessions
                    .GroupBy(s => s.Start.ToLocalTime().ToString("yyyy-MM-dd HH:00"))
                    .Select(g => new[]
                    {
                        g.First().Start.ToLocalTime().ToString("yyyy-MM-dd"),
                        g.First().Start.ToLocalTime().ToString("HH:mm"),
                        g.First().Start.ToLocalTime().AddHours(1).ToString("HH:mm"),
                        g.Where(x => x.State == "active").Sum(x => x.DurationSeconds).ToString(),
                        g.Where(x => x.State == "idle").Sum(x => x.DurationSeconds).ToString(),
                        g.Where(x => x.State == "locked").Sum(x => x.DurationSeconds).ToString(),
                        g.Where(x => x.State == "unknown").Sum(x => x.DurationSeconds).ToString(),
                        Math.Max(0, g.Count() - 1).ToString()
                    }))
            .ToList();

    static List<string[]> Apps(AnalysisResult r) =>
        new List<string[]>
        {
            new[]
            {
                "Приложение",
                "Активное время, сек",
                "Человекочитаемое время",
                "Доля",
                "Количество сессий",
                "Средняя сессия",
                "Максимальная сессия",
                "Уникальных окон",
                "Переключений в приложение"
            }
        }
            .Concat(
                r.Sessions
                    .Where(s => s.State == "active")
                    .GroupBy(s => s.ProcessName)
                    .Select(g => new[]
                    {
                        g.Key,
                        g.Sum(x => x.DurationSeconds).ToString(),
                        Fmt(g.Sum(x => x.DurationSeconds)),
                        (g.Sum(x => x.DurationSeconds) / Math.Max(1, r.ActiveSeconds)).ToString(),
                        g.Count().ToString(),
                        g.Average(x => x.DurationSeconds).ToString(),
                        g.Max(x => x.DurationSeconds).ToString(),
                        g.Select(x => x.WindowTitle).Distinct().Count().ToString(),
                        r.Chains2.Count(x => x.Key.EndsWith("→ " + g.Key, StringComparison.OrdinalIgnoreCase)).ToString()
                    }))
            .ToList();

    static List<string[]> Ops(AnalysisResult r) =>
        new List<string[]>
        {
            new[]
            {
                "Операция",
                "Категория",
                "Приложение",
                "Заголовок",
                "Суммарное время",
                "Количество повторений",
                "Средняя длительность",
                "Пользователи",
                "Компьютеры"
            }
        }
            .Concat(
                r.Sessions
                    .GroupBy(s => new { s.ProcessName, s.WindowTitle, s.ActivityCategory })
                    .Select(g => new[]
                    {
                        g.Key.WindowTitle,
                        g.Key.ActivityCategory,
                        g.Key.ProcessName,
                        g.Key.WindowTitle,
                        g.Sum(x => x.DurationSeconds).ToString(),
                        g.Count().ToString(),
                        g.Average(x => x.DurationSeconds).ToString(),
                        string.Join(",", g.Select(x => x.UserName).Distinct()),
                        string.Join(",", g.Select(x => x.MachineName).Distinct())
                    }))
            .ToList();

    static List<string[]> Sessions(AnalysisResult r) =>
        new List<string[]>
        {
            new[]
            {
                "Начало",
                "Окончание",
                "Длительность, сек",
                "Человекочитаемая длительность",
                "Состояние",
                "Приложение",
                "Заголовок окна",
                "Компьютер",
                "Пользователь",
                "Скриншоты"
            }
        }
            .Concat(
                r.Sessions.Select(s => new[]
                {
                    s.Start.ToString("O"),
                    s.End.ToString("O"),
                    s.DurationSeconds.ToString(),
                    Fmt(s.DurationSeconds),
                    s.State,
                    s.ProcessName,
                    s.WindowTitle,
                    s.MachineName,
                    s.UserName,
                    s.ScreenshotCount.ToString()
                }))
            .ToList();

    static List<string[]> Chains(AnalysisResult r) =>
        new List<string[]>
        {
            new[]
            {
                "Цепочка",
                "Количество повторений",
                "Дней наблюдения",
                "Частота в день",
                "Средняя длительность",
                "Суммарное время",
                "Пользователи",
                "Компьютеры"
            }
        }
            .Concat(
                r.Chains2.Select(c => new[]
                {
                    c.Key,
                    c.Value.ToString(),
                    Math.Max(1, r.DataQuality!.Days).ToString(),
                    (c.Value / (double)Math.Max(1, r.DataQuality!.Days)).ToString(),
                    "0",
                    "0",
                    r.DataQuality.Users.ToString(),
                    r.DataQuality.Machines.ToString()
                }))
            .ToList();

    static List<string[]> Opps(AnalysisResult r) =>
        new List<string[]>
        {
            new[]
            {
                "Цепочка",
                "Приложения",
                "Повторения",
                "Дней наблюдения",
                "Частота в день",
                "Среднее время",
                "Суммарное время",
                "Стабильность",
                "Уровень доказательности",
                "Score",
                "Предварительная гипотеза",
                "Обоснование",
                "Следующий шаг"
            }
        }
            .Concat(
                r.AutomationOpportunities.Select(o => new[]
                {
                    o.Name,
                    o.Applications,
                    "",
                    Math.Max(1, r.DataQuality!.Days).ToString(),
                    o.FrequencyPerDay.ToString(),
                    o.AverageDurationSeconds.ToString(),
                    o.TotalSeconds.ToString(),
                    o.Stability.ToString(),
                    o.PotentialCategory,
                    o.AutomationScore.ToString(),
                    o.RecommendedSolution,
                    o.Rationale,
                    "Интервью и валидация"
                }))
            .ToList();

    static List<string[]> Errors(AnalysisResult r) =>
        new List<string[]>
        {
            new[] { "Ошибка", "Значение" }
        }
            .Concat(r.Errors.Select(e => new[] { e, "" }))
            .ToList();

    static List<string[]> Raw(AnalysisResult r) =>
        new List<string[]>
        {
            new[] { "timestampUtc", "machine", "user", "eventType", "process", "title" }
        }
            .Concat(r.Events.Select(e => new[] { e.TimestampUtc.ToString("O"), e.MachineName, e.UserName, e.EventType, e.ProcessName, e.WindowTitle }))
            .ToList();

    static string ChartXml() =>
        "<c:chartSpace xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\"><c:chart><c:plotArea><c:barChart><c:barDir val=\"bar\"/><c:ser><c:idx val=\"0\"/><c:order val=\"0\"/><c:tx><c:v>Структура времени</c:v></c:tx><c:cat><c:strRef><c:f>Дашборд!$A$5:$A$8</c:f></c:strRef></c:cat><c:val><c:numRef><c:f>Дашборд!$B$5:$B$8</c:f></c:numRef></c:val></c:ser></c:barChart></c:plotArea></c:chart></c:chartSpace>";

    static string Cells(string[] v) =>
        string.Concat(v.Select(x => double.TryParse(x, out _) ? $"<c><v>{Esc(x)}</v></c>" : $"<c t=\"inlineStr\"><is><t>{Esc(x)}</t></is></c>"));

    static string Col(int n)
    {
        var s = "";
        while (n > 0)
        {
            n--;
            s = (char)('A' + n % 26) + s;
            n /= 26;
        }

        return s;
    }

    static string Fmt(double s) => TimeSpan.FromSeconds(Math.Max(0, s)).ToString(@"hh\:mm\:ss");

    static string Esc(string? s) => System.Security.SecurityElement.Escape(s ?? "") ?? "";

    static void Add(ZipArchive z, string n, string c)
    {
        var e = z.CreateEntry(n);
        using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
        w.Write(c);
    }
}
