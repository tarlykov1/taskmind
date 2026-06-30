using GSPTaskMiningAnalyzer;
using GSPTaskMiningAnalyzer.Models;

Options opt;
try
{
    opt = Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

if (opt.Debug) Console.WriteLine($"Input={opt.Input} Output={opt.Output} TimeZone={opt.ReportTimeZone.Id}");
var (events, errors) = new LogReader().Read(opt.Input);
if (opt.Machine is not null) events = events.Where(e => e.MachineName.Equals(opt.Machine, StringComparison.OrdinalIgnoreCase)).ToList();
if (opt.User is not null) events = events.Where(e => e.UserName.Equals(opt.User, StringComparison.OrdinalIgnoreCase)).ToList();
if (opt.From is not null) events = events.Where(e => e.TimestampUtc >= opt.From).ToList();
if (opt.To is not null) events = events.Where(e => e.TimestampUtc <= opt.To).ToList();
var result = new StatisticsService().Analyze(events);
result.Errors.AddRange(errors);

var generation = new ReportGenerationService().Generate(
    result,
    opt.Output,
    opt.IncludeRaw,
    opt.HtmlOnly,
    opt.XlsxOnly,
    input: opt.Input,
    arguments: string.Join(" ", Environment.GetCommandLineArgs().Skip(1)),
    reportTimeZone: opt.ReportTimeZone);
var xlsxPath = generation.XlsxPath;
var htmlPath = generation.HtmlPath;

Console.WriteLine(opt.HtmlOnly ? "XLSX: пропущен" : xlsxPath is null ? "XLSX: ошибка" : $"XLSX: создан {xlsxPath}");
Console.WriteLine(opt.XlsxOnly ? "HTML: пропущен" : htmlPath is null ? "HTML: ошибка" : $"HTML: создан {htmlPath}");
if (generation.DiagnosticLogPath is not null) Console.WriteLine("Diagnostic log: " + generation.DiagnosticLogPath);

var missingRequired = (!opt.HtmlOnly && xlsxPath is null) || (!opt.XlsxOnly && htmlPath is null);
return errors.Count > 0 || missingRequired ? 1 : 0;

static Options Parse(string[] a)
{
    var o = new Options();
    for (int i = 0; i < a.Length; i++)
    {
        string? N() => i + 1 < a.Length ? a[++i] : null;
        switch (a[i])
        {
            case "--input": o.Input = N() ?? o.Input; break;
            case "--output": o.Output = N() ?? o.Output; break;
            case "--from": if (DateTimeOffset.TryParse(N(), out var f)) o.From = f; break;
            case "--to": if (DateTimeOffset.TryParse(N(), out var t)) o.To = t; break;
            case "--machine": o.Machine = N(); break;
            case "--user": o.User = N(); break;
            case "--include-raw": o.IncludeRaw = true; break;
            case "--debug": o.Debug = true; break;
            case "--html-only": o.HtmlOnly = true; break;
            case "--xlsx-only": o.XlsxOnly = true; break;
            case "--self-test": o.Input = CreateSample(); o.Output = Path.Combine(Path.GetTempPath(), "GSPTaskMiningAnalyzerSelfTest", Guid.NewGuid().ToString("N")); break;
            case "--timezone": o.ReportTimeZone = ParseTimeZone(N()); break;
        }
    }
    if (o.HtmlOnly && o.XlsxOnly) throw new ArgumentException("--html-only and --xlsx-only cannot be used together.");
    return o;
}

static TimeZoneInfo ParseTimeZone(string? value)
{
    if (string.IsNullOrWhiteSpace(value) ||
        value.Equals("local", StringComparison.OrdinalIgnoreCase))
    {
        return TimeZoneInfo.Local;
    }

    if (value.Equals("utc", StringComparison.OrdinalIgnoreCase))
    {
        return TimeZoneInfo.Utc;
    }

    try
    {
        return TimeZoneInfo.FindSystemTimeZoneById(value);
    }
    catch (TimeZoneNotFoundException ex)
    {
        throw new ArgumentException(
            $"Unknown timezone '{value}'. Use 'local', 'utc', or a system timezone ID, for example 'Russian Standard Time' on Windows or 'Europe/Moscow' on Linux.",
            ex);
    }
    catch (InvalidTimeZoneException ex)
    {
        throw new ArgumentException(
            $"Invalid timezone '{value}'. Use 'local', 'utc', or a valid system timezone ID, for example 'Russian Standard Time' on Windows or 'Europe/Moscow' on Linux.",
            ex);
    }
}

static string CreateSample()
{
    var d = Path.Combine(Path.GetTempPath(), "GSPTaskMiningAnalyzerSample", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(d);
    File.WriteAllText(Path.Combine(d, "events.jsonl"), "{\"eventType\":\"active_window_tick\",\"timestampUtc\":\"2026-01-01T00:00:00Z\",\"timestampLocal\":\"2026-01-01T00:00:00+00:00\",\"machineName\":\"m\",\"userName\":\"u\",\"processName\":\"Excel\",\"windowTitle\":\"Book1\",\"isIdle\":false,\"durationSeconds\":5}\n");
    return d;
}

sealed class Options
{
    public string Input = "data";
    public string Output = "reports";
    public DateTimeOffset? From;
    public DateTimeOffset? To;
    public string? Machine;
    public string? User;
    public bool IncludeRaw;
    public bool Debug;
    public bool HtmlOnly;
    public bool XlsxOnly;
    public TimeZoneInfo ReportTimeZone = TimeZoneInfo.Local;
}
