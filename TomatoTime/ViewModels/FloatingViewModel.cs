using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TomatoTime.Models;
using TomatoTime.Services;

namespace TomatoTime.ViewModels;

/// <summary>悬浮窗 VM:倒计时 + 当前任务名 + 展开按钮。</summary>
public partial class FloatingViewModel : ObservableObject
{
    private readonly ITimerService _timer;
    private readonly ITaskService _tasks;
    private readonly Dispatcher _dispatcher;

    [ObservableProperty] private string remainingText = "25:00";
    [ObservableProperty] private string taskTitle = "";
    [ObservableProperty] private bool isRunning;

    public FloatingViewModel(ITimerService timer, ITaskService tasks, Dispatcher d)
    {
        _timer = timer;
        _tasks = tasks;
        _dispatcher = d;
        _timer.Tick += (_, _) => _dispatcher.BeginInvoke(Refresh);
        Refresh();
    }

    private void Refresh()
    {
        var r = _timer.State.RemainingSeconds;
        RemainingText = $"{r / 60:00}:{r % 60:00}";
        IsRunning = _timer.State.Status is TimerStatus.Working or TimerStatus.Break;
        _ = UpdateTaskTitleAsync();
    }

    private async Task UpdateTaskTitleAsync()
    {
        var active = await _tasks.GetActiveAsync();
        TaskTitle = active?.Title ?? "";
    }

    [RelayCommand]
    private void Expand()
    {
        App.Services.GetRequiredService<IWindowService>().ShowMain();
    }
}
