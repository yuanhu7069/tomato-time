using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TomatoTime.Services;

namespace TomatoTime.ViewModels;

/// <summary>今日待办 + 今日已完成两个列表,跨天自动清零刷新。</summary>
public partial class TasksViewModel : ObservableObject
{
    private readonly ITaskService _svc;
    private readonly Dispatcher _dispatcher;
    private DateTime _lastRefreshDate = DateTime.MinValue;

    [ObservableProperty] private bool showCompleted;
    [ObservableProperty] private int completedCount;
    [ObservableProperty] private string newTaskTitle = "";

    public ObservableCollection<TaskRow> Pending { get; } = new();
    public ObservableCollection<TaskRow> Completed { get; } = new();

    /// <summary>列表刷新完成后触发(供主窗同步激活任务标题)。</summary>
    public event Action? TasksChanged;

    public TasksViewModel(ITaskService svc, Dispatcher d)
    {
        _svc = svc;
        _dispatcher = d;
        _ = RefreshAsync();
    }

    /// <summary>待办行模型:支持行内编辑(IsEditing 切换标题输入框)与标题变更通知。</summary>
    public partial class TaskRow : ObservableObject
    {
        public int Id { get; }
        public int TodayPomodoros { get; }
        public bool IsActive { get; }

        [ObservableProperty] private string title;
        [ObservableProperty] private bool isEditing;

        public TaskRow(int id, string title, int todayPomodoros, bool isActive)
        {
            Id = id;
            Title = title;
            TodayPomodoros = todayPomodoros;
            IsActive = isActive;
        }
    }
    public async Task RefreshAsync()
    {
        if (DateTime.Today != _lastRefreshDate) _lastRefreshDate = DateTime.Today;
        var pending = await _svc.GetTodayPendingAsync();
        var done = await _svc.GetTodayCompletedAsync();
        var counts = await _svc.GetPomodorosCountsAsync(DateTime.Today);
        _dispatcher.BeginInvoke(() =>
        {
            Pending.Clear();
            foreach (var t in pending)
                Pending.Add(new TaskRow(t.Id, t.Title, counts.GetValueOrDefault(t.Id), t.IsActive));
            Completed.Clear();
            foreach (var t in done)
                Completed.Add(new TaskRow(t.Id, t.Title, counts.GetValueOrDefault(t.Id), t.IsActive));
            CompletedCount = Completed.Count;
            TasksChanged?.Invoke();
        });
    }

    [RelayCommand]
    private async Task Add()
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle)) return;
        await _svc.CreateAsync(NewTaskTitle);
        NewTaskTitle = "";
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ToggleComplete(TaskRow row)
    {
        await _svc.CompleteAsync(row.Id);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task Activate(TaskRow row)
    {
        await _svc.ActivateAsync(row.Id);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task Delete(TaskRow row)
    {
        await _svc.DeleteAsync(row.Id);
        await RefreshAsync();
    }

    [RelayCommand]
    private void BeginEdit(TaskRow row) => row.IsEditing = true;

    [RelayCommand]
    private async Task SaveEdit(TaskRow row)
    {
        var t = row.Title?.Trim();
        if (string.IsNullOrWhiteSpace(t))
        {
            row.IsEditing = false;
            return;
        }
        if (t != row.Title) await _svc.UpdateTitleAsync(row.Id, t);
        row.IsEditing = false;
        await RefreshAsync();
    }

    [RelayCommand]
    private void CancelEdit(TaskRow row) => row.IsEditing = false;
}
