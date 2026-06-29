using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace GSPTaskMiningAgent;

public sealed class TrayIconService : IDisposable
{
    private readonly AgentPaths _paths;
    private readonly NotifyIcon _notifyIcon;
    private Icon _currentIcon;
    public TrayIconService(AgentPaths paths)
    {
        _paths = paths;
        TrayIconResources.ConfigureErrorDirectory(paths.Errors);
        _currentIcon = TrayIconResources.Load(TrayIconState.Green);
        _notifyIcon = new NotifyIcon { Icon = _currentIcon, Text = BuildTooltip("зелёный – сбор работает"), Visible = true, ContextMenuStrip = BuildMenu() };
    }
    public ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Состояние агента", null, (_, _) => MessageBox.Show(BuildTooltip("Состояние"), "GSP Task Mining Agent"));
        menu.Items.Add("Открыть папку данных", null, (_, _) => Open(_paths.Root));
        menu.Items.Add("Открыть текущий лог", null, (_, _) => Open(_paths.Logs));
        menu.Items.Add("Создать отчёт");
        menu.Items.Add("Приостановить сбор", null, (_, _) => File.WriteAllText(Path.Combine(_paths.Root, "paused.flag"), DateTimeOffset.UtcNow.ToString("O")));
        menu.Items.Add("Возобновить сбор", null, (_, _) => { var f=Path.Combine(_paths.Root,"paused.flag"); if(File.Exists(f)) File.Delete(f); });
        menu.Items.Add("Скриншоты: включены/выключены");
        menu.Items.Add("Начать операцию");
        menu.Items.Add("Завершить операцию");
        menu.Items.Add("Выбрать процесс");
        menu.Items.Add("Остановить агент", null, (_, _) => GracefulShutdown());
        menu.Items.Add("О программе", null, (_, _) => MessageBox.Show("GSP Task Mining Agent", "О программе"));
        return menu;
    }
    public string BuildTooltip(string state) => $"GSP Task Mining Agent\n{state}\nВремя последнего события: {DateTimeOffset.Now:dd.MM.yyyy HH:mm:ss}\nСобытий сегодня: {CountToday()}\nКоличество ошибок: {CountErrors()}";
    public void SetState(string state) { _notifyIcon.Text = BuildTooltip(state)[..Math.Min(63, BuildTooltip(state).Length)]; var nextState = state.Contains("ошибка",StringComparison.OrdinalIgnoreCase) ? TrayIconState.Red : state.Contains("выключ",StringComparison.OrdinalIgnoreCase) ? TrayIconState.Gray : state.Contains("приост",StringComparison.OrdinalIgnoreCase) ? TrayIconState.Yellow : TrayIconState.Green; ReplaceIcon(nextState); }
    private void ReplaceIcon(TrayIconState state) { var previous = _currentIcon; _currentIcon = TrayIconResources.Load(state); _notifyIcon.Icon = _currentIcon; previous.Dispose(); }
    public void GracefulShutdown() => File.WriteAllText(_paths.StopFile, DateTimeOffset.UtcNow.ToString("O"));
    private int CountToday()=>Directory.Exists(_paths.Logs)?Directory.EnumerateFiles(_paths.Logs,$"events-{DateTimeOffset.UtcNow:yyyyMMdd}.jsonl").Select(f=>File.ReadLines(f).Count()).FirstOrDefault():0;
    private int CountErrors()=>Directory.Exists(_paths.Errors)?Directory.EnumerateFiles(_paths.Errors).Count():0;
    private static void Open(string path){try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch{}}
    public void Dispose(){_notifyIcon.Visible=false;_notifyIcon.Dispose();_currentIcon.Dispose();}
}
