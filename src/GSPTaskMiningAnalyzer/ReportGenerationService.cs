using System.Text;
using GSPTaskMiningAnalyzer.Models;

namespace GSPTaskMiningAnalyzer;

public sealed record ReportGenerationResult(string? XlsxPath, string? HtmlPath, string? DiagnosticLogPath, IReadOnlyList<string> Errors);

public sealed class ReportGenerationService
{
    public ReportGenerationResult Generate(
        AnalysisResult result,
        string outputDirectory,
        bool includeRaw,
        bool htmlOnly = false,
        bool xlsxOnly = false,
        Func<AnalysisResult, string, bool, string>? writeExcel = null,
        Func<AnalysisResult, string, string>? writeHtml = null,
        string input = "data",
        string arguments = "")
    {
        string? xlsxPath = null;
        string? htmlPath = null;
        var generationErrors = new List<string>();

        if (!htmlOnly)
        {
            try { xlsxPath = (writeExcel ?? ((r, o, raw) => new ExcelReportService().Write(r, o, raw)))(result, outputDirectory, includeRaw); }
            catch (Exception ex) { generationErrors.Add("Ошибка создания Excel: " + ex); }
        }

        if (!xlsxOnly)
        {
            try { htmlPath = (writeHtml ?? ((r, o) => new HtmlReportService().Write(r, o)))(result, outputDirectory); }
            catch (Exception ex) { generationErrors.Add("Ошибка создания HTML: " + ex); }
        }

        var diagnosticLog = generationErrors.Count == 0 ? null : WriteDiagnosticLog(outputDirectory, input, arguments, result, generationErrors);
        return new ReportGenerationResult(xlsxPath, htmlPath, diagnosticLog, generationErrors);
    }

    public static string WriteDiagnosticLog(string outputDirectory, string input, string arguments, AnalysisResult result, IReadOnlyList<string> errors)
    {
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, $"analyzer-error-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var sb = new StringBuilder();
        sb.AppendLine("Дата и время: " + DateTimeOffset.Now.ToString("O"));
        sb.AppendLine("Параметры запуска: " + arguments);
        sb.AppendLine("input: " + input);
        sb.AppendLine("output: " + outputDirectory);
        sb.AppendLine("Количество событий: " + (result.Events?.Count ?? 0));
        sb.AppendLine("Количество сессий: " + (result.Sessions?.Count ?? 0));
        sb.AppendLine();
        sb.AppendLine("Исключения:");
        foreach (var error in errors) sb.AppendLine(error).AppendLine();
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return path;
    }
}
