using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TomatoTime.Models;
using TomatoTime.Services;

namespace TomatoTime.ViewModels;

/// <summary>主窗 VM:顶部计时视图 + 任务面板 + 统计页的宿主。</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ITimerService _timer;
    private readonly ITaskService _tasks;
    private readonly Dispatcher _dispatcher;
    private DateTime _lastDay = DateTime.Today;

    [ObservableProperty] private string remainingText = "25:00";
    [ObservableProperty] private string phaseLabel = "工作";
    [ObservableProperty] private string currentTaskTitle = "(未选择任务)";
    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private bool isIdle = true;
    [ObservableProperty] private bool isPaused;
    [ObservableProperty] private bool canPause;
    [ObservableProperty] private bool canControl;

    public TasksViewModel Tasks { get; }
    public StatsViewModel Stats { get; }

    public MainViewModel(ITimerService timer, ITaskService tasks, IStatsService stats, Dispatcher dispatcher)
    {
        _timer = timer;
        _tasks = tasks;
        _dispatcher = dispatcher;
        Tasks = new TasksViewModel(tasks, dispatcher);
        Tasks.TasksChanged += () => _ = UpdateTaskTitleAsync();
        Stats = new StatsViewModel(stats, tasks);

        _timer.Tick += (_, _) => _dispatcher.BeginInvoke(RefreshDisplay);
        _timer.PhaseStarted += (_, e) => _dispatcher.BeginInvoke(() =>
        {
            PhaseLabel = LabelFor(e.Phase);
            _ = UpdateTaskTitleAsync();
            Tasks.RefreshAsync();
        });
        _timer.PhaseEnded += (_, _) => _dispatcher.BeginInvoke(() => Tasks.RefreshAsync());
        _timer.Skipped += (_, _) => _dispatcher.BeginInvoke(() => Tasks.RefreshAsync());

        RefreshDisplay();
        _ = UpdateTaskTitleAsync();
    }

    private void RefreshDisplay()
    {
        var r = _timer.State.RemainingSeconds;
        RemainingText = $"{r / 60:00}:{r % 60:00}";
        var s = _timer.State.Status;
        IsRunning = s is TimerStatus.Working or TimerStatus.Break or TimerStatus.Paused;
        IsIdle = s == TimerStatus.Idle;
        IsPaused = s == TimerStatus.Paused;
        CanPause = s is TimerStatus.Working or TimerStatus.Break;
        CanControl = s != TimerStatus.Idle;
        if (DateTime.Today != _lastDay)
        {
            // 跨天:今日待办/已完成按本地日期重新清零
            _lastDay = DateTime.Today;
            Tasks.RefreshAsync();
        }
    }

    private async Task UpdateTaskTitleAsync()
    {
        var active = await _tasks.GetActiveAsync();
        CurrentTaskTitle = active?.Title ?? "(未选择任务)";
    }

    private static string LabelFor(PhaseKind p) => p switch
    {
        PhaseKind.Work => "工作",
        PhaseKind.ShortBreak => "短休",
        _ => "长休"
    };

    [RelayCommand] private void Start() => _timer.Start();
    [RelayCommand] private void Pause() => _timer.Pause();
    [RelayCommand] private void Resume() => _timer.Resume();
    [RelayCommand] private void Skip() => _timer.Skip();
    [RelayCommand] private void Stop() => _timer.Stop();

    [RelayCommand]
    private void OpenSettings() => App.Services.GetRequiredService<IWindowService>().ShowSettings();
}
