using System.Drawing;
using System.Windows.Forms;

namespace GSPTaskMiningAgent;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;
    public TrayApplicationContext(AgentPaths paths, string[] args)
    {
        paths.EnsureAll();
        _notifyIcon = new NotifyIcon { Icon = TrayIconResources.Load(TrayIconState.Green), Text = "GSP Task Mining: работает", Visible = true, ContextMenuStrip = BuildMenu(paths) };
        _worker = Task.Run(() => Program.RunAgentLoop(paths, args.Contains("--once", StringComparer.OrdinalIgnoreCase), _cts.Token));
        _worker.ContinueWith(_ => ExitThread());
    }
    private ContextMenuStrip BuildMenu(AgentPaths paths){var m=new ContextMenuStrip();m.Items.Add("Состояние: работает");m.Items.Add("Последнее событие");m.Items.Add("Открыть папку данных",null,(_,_)=>System.Diagnostics.Process.Start("explorer.exe",paths.Data));m.Items.Add("Открыть текущий журнал");m.Items.Add("Создать отчёт");m.Items.Add("Приостановить сбор");m.Items.Add("Возобновить сбор");m.Items.Add("Скриншоты: включены/выключены");m.Items.Add("Остановить агент",null,(_,_)=>{File.WriteAllText(paths.StopFile,DateTimeOffset.UtcNow.ToString("O"));ExitThread();});m.Items.Add("О программе");return m;}
    protected override void Dispose(bool disposing){if(disposing){_cts.Cancel();_notifyIcon.Text="GSP Task Mining: остановка";
            _notifyIcon.Icon = TrayIconResources.Load(TrayIconState.Gray);_notifyIcon.Visible=false;_notifyIcon.Dispose();_cts.Dispose();}base.Dispose(disposing);} 
}
