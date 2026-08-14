using System.Windows;
using TomatoTime.Models;
using TomatoTime.ViewModels;
using TomatoTime.Views;

namespace TomatoTime.Services;

/// <summary>
/// 订阅 PhaseEnded 弹遮罩 + 通知 + 响铃;按钮响应转发给 ITimerService。
/// Skipped 事件不被订阅 → 跳过不弹遮罩。
/// </summary>
public class OverlayService : IOverlayService
{
    private readonly ITimerService _timer;
    private readonly INotificationService _notify;
    private readonly ISettingsService _settings;
    private readonly ITaskService _tasks;
    private OverlayWindow? _window;
    private bool _bellStarted;

    public OverlayService(ITimerService timer, INotificationService notify, ISettingsService settings, ITaskService tasks)
    {
        _timer = timer;
        _notify = notify;
        _settings = settings;
        _tasks = tasks;
        _timer.PhaseEnded += (_, e) => Application.Current.Dispatcher.BeginInvoke(() => Show(e));
    }

    private void Show(PhaseEventArgs e)
    {
        _notify.Notify("TomatoTime", e.Phase == PhaseKind.Work ? "工作段结束!" : "休息结束!");
        _notify.StartBell();
        _bellStarted = true;

        // 写 WorkSession 流水:仅 Working 段自然归 0(PhaseStartedAt 非空);稍后期到不重复写
        if (e.Phase == PhaseKind.Work && e.PhaseStartedAt is { } started)
            _ = RecordAsync(started);

        _window ??= new OverlayWindow();
        var vm = new OverlayViewModel(_timer, this, _settings);
        vm.Configure(e.Phase);
        _window.DataContext = vm;
        _window.Show();
        _window.Activate();
    }

    private async Task RecordAsync(DateTime started)
    {
        try
        {
            var now = DateTime.UtcNow;
            var active = await _tasks.GetActiveAsync();
            await _tasks.RecordWorkSessionAsync(active?.Id, started, now,
                Math.Max(0, (int)(now - started).TotalSeconds));
        }
        catch
        {
            // 流水写入失败不影响提醒流程
        }
    }

    public void OnStartNext()
    {
        StopBellAndClose();
        _timer.StartNext();
    }

    public void OnPostpone()
    {
        StopBellAndClose();
        _timer.Postpone();
    }

    private void StopBellAndClose()
    {
        if (_bellStarted)
        {
            _notify.StopBell();
            _bellStarted = false;
        }
        _window?.Close();
        _window = null;
    }
}
