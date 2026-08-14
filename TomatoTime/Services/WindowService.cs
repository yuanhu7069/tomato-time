using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TomatoTime.ViewModels;
using TomatoTime.Views;

namespace TomatoTime.Services;

/// <summary>协调主窗 ↔ 托盘 ↔ 悬浮 ↔ 设置窗的显示/隐藏与退出。</summary>
public class WindowService : IWindowService
{
    private readonly IServiceProvider _sp;
    private readonly ITimerService _timer;
    private readonly IStatePersistenceService _persist;
    private readonly IFloatingService _floating;
    private readonly INotificationService _notify;
    private MainWindow? _main;
    private SettingsWindow? _settings;

    public WindowService(IServiceProvider sp, ITimerService timer, IStatePersistenceService persist,
        IFloatingService floating, INotificationService notify)
    {
        _sp = sp;
        _timer = timer;
        _persist = persist;
        _floating = floating;
        _notify = notify;
    }

    public void ShowMain()
    {
        _main ??= new MainWindow
        {
            DataContext = new MainViewModel(
                _sp.GetRequiredService<ITimerService>(),
                _sp.GetRequiredService<ITaskService>(),
                _sp.GetRequiredService<IStatsService>(),
                Application.Current.Dispatcher)
        };
        _main.Show();
        _main.Activate();
    }

    public void HideMain() => _main?.Hide();

    public void ToggleMain()
    {
        if (_main?.IsVisible == true) HideMain();
        else ShowMain();
    }

    public void ShowFloating() => _floating.Show();

    public void ShowSettings()
    {
        if (_settings == null)
        {
            _settings = new SettingsWindow
            {
                DataContext = new SettingsViewModel(_sp.GetRequiredService<ISettingsService>()),
                Owner = _main
            };
            _settings.Closed += (_, _) => _settings = null;
        }
        _settings.Show();
        _settings.Activate();
    }

    public void OnExit()
    {
        _notify.StopBell();
        // 合并保存计时状态 + 悬浮窗坐标,一次写文件
        _persist.Save(_timer.State, _floating.Left, _floating.Top);
        _floating.Close();
        _settings?.Close();
        Application.Current.Shutdown();
    }
}
