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

    /// <summary>新增任务的时长选项(分钟)。</summary>
    public List<int> LengthOptions { get; } = new() { 25, 30, 45, 60 };

    /// <summary>新增任务选择的每番茄时长(分钟)。</summary>
    [ObservableProperty] private int selectedLength = 25;

    /// <summary>新增任务选择的计划番茄数(默认 1)。</summary>
    [ObservableProperty] private string plannedPomodorosText = "1";

    /// <summary>当前新增任务解析出的计划番茄数(非法输入回落 1)。</summary>
    public int PlannedPomodoros => int.TryParse(PlannedPomodorosText, out var n) && n >= 1 ? n : 1;

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
        /// <summary>计划番茄数(>1 时显示计划进度)。</summary>
        public int PlannedPomodoros { get; }
        /// <summary>每番茄时长(分钟,用于显示)。</summary>
        public int? LengthMinutes { get; }

        /// <summary>番茄进度文本(如今天完成了 2/4 计划)。</summary>
        public string PomodoroText => PlannedPomodoros > 1
            ? $"{TodayPomodoros}/{PlannedPomodoros}"
            : TodayPomodoros.ToString();

        [ObservableProperty] private string title;
        [ObservableProperty] private bool isEditing;

        public TaskRow(int id, string title, int todayPomodoros, bool isActive, int plannedPomodoros, int? lengthMinutes)
        {
            Id = id;
            Title = title;
            TodayPomodoros = todayPomodoros;
            IsActive = isActive;
            PlannedPomodoros = plannedPomodoros;
            LengthMinutes = lengthMinutes;
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
                Pending.Add(new TaskRow(t.Id, t.Title, counts.GetValueOrDefault(t.Id), t.IsActive,
                    t.PlannedPomodoros, t.PomodoroLengthMinutes));
            Completed.Clear();
            foreach (var t in done)
                Completed.Add(new TaskRow(t.Id, t.Title, counts.GetValueOrDefault(t.Id), t.IsActive,
                    t.PlannedPomodoros, t.PomodoroLengthMinutes));
            CompletedCount = Completed.Count;
            TasksChanged?.Invoke();
        });
    }

    [RelayCommand]
    private async Task Add()
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle)) return;
        await _svc.CreateAsync(NewTaskTitle, SelectedLength, PlannedPomodoros);
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
