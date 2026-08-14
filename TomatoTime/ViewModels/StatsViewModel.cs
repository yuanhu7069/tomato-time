using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using TomatoTime.Services;

namespace TomatoTime.ViewModels;

public enum StatsRange { Day, Week, Month }

/// <summary>统计页 VM:日/周/月只读视图,数据来自 WorkSession 流水临时聚合。</summary>
public partial class StatsViewModel : ObservableObject
{
    private readonly IStatsService _svc;
    private readonly ITaskService _tasks;
    private DateTime _anchor = DateTime.Today;
    private StatsRange _range = StatsRange.Day;

    [ObservableProperty] private string viewTitle = "日视图";
    [ObservableProperty] private string pomodoroKpi = "0";
    [ObservableProperty] private string durationKpi = "0h 0m";
    [ObservableProperty] private string extraKpi = "";
    [ObservableProperty] private string dailyAvgKpi = "";

    private ISeries[] _series = Array.Empty<ISeries>();
    public ISeries[] Series
    {
        get => _series;
        private set => SetProperty(ref _series, value);
    }

    private Axis[] _xAxes = { new() { Labels = Array.Empty<string>() } };
    public Axis[] XAxes
    {
        get => _xAxes;
        private set => SetProperty(ref _xAxes, value);
    }

    public ObservableCollection<TaskBreakdown> BreakdownRows { get; } = new();

    public StatsViewModel(IStatsService svc, ITaskService tasks)
    {
        _svc = svc;
        _tasks = tasks;
        _ = LoadAsync();
    }

    public async Task SetRangeAsync(StatsRange range)
    {
        _range = range;
        _anchor = DateTime.Today;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task Prev()
    {
        _anchor = _range switch
        {
            StatsRange.Day => _anchor.AddDays(-1),
            StatsRange.Week => _anchor.AddDays(-7),
            _ => _anchor.AddMonths(-1)
        };
        await LoadAsync();
    }

    [RelayCommand]
    private async Task Next()
    {
        _anchor = _range switch
        {
            StatsRange.Day => _anchor.AddDays(1),
            StatsRange.Week => _anchor.AddDays(7),
            _ => _anchor.AddMonths(1)
        };
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        switch (_range)
        {
            case StatsRange.Day: await LoadDayAsync(_anchor); break;
            case StatsRange.Week: await LoadWeekAsync(StartOfWeek(_anchor)); break;
            default: await LoadMonthAsync(_anchor.Year, _anchor.Month); break;
        }
    }

    private async Task LoadDayAsync(DateTime day)
    {
        ViewTitle = $"日视图 · {day:MM-dd}";
        PomodoroKpi = (await _svc.GetPomodorosForAsync(day)).ToString();
        DurationKpi = FormatDuration(await _svc.GetTotalSecondsForAsync(day));
        var active = await _tasks.GetActiveAsync();
        ExtraKpi = "当前任务: " + (active?.Title ?? "(未选择)");
        DailyAvgKpi = "";

        var buckets = await _svc.GetHourlyBucketsAsync(day);
        Series = new ISeries[]
        {
            new ColumnSeries<int>
            {
                Values = buckets,
                Name = "番茄",
                Fill = new SolidColorPaint(new SKColor(218, 54, 48))
            }
        };
        XAxes = new Axis[]
        {
            new()
            {
                Labels = Enumerable.Range(0, 24)
                    .Select(h => h % 3 == 0 ? $"{h}:00" : "").ToArray(),
                LabelsRotation = 0
            }
        };
        await FillBreakdownAsync(await _svc.GetBreakdownForDayAsync(day));
    }

    private async Task LoadWeekAsync(DateTime weekStart)
    {
        var week = await _svc.GetWeeklyAsync(weekStart);
        ViewTitle = $"周视图 · {weekStart:MM-dd} ~ {weekStart.AddDays(6):MM-dd}";
        var total = week.Sum(x => x.Count);
        var sec = week.Sum(x => x.TotalSeconds);
        PomodoroKpi = total.ToString();
        DurationKpi = FormatDuration(sec);
        DailyAvgKpi = $"日均 {total / 7.0:0.0}";
        ExtraKpi = $"最长连续 {await _svc.GetStreakDaysAsync(weekStart, weekStart.AddDays(6))} 天";

        Series = new ISeries[]
        {
            new ColumnSeries<int>
            {
                Values = week.Select(x => x.Count).ToArray(),
                Name = "番茄",
                Fill = new SolidColorPaint(new SKColor(218, 54, 48))
            }
        };
        XAxes = new Axis[]
        {
            new() { Labels = week.Select(x => $"{x.Date:MM-dd}").ToArray(), LabelsRotation = 30 }
        };
        await FillBreakdownAsync(await _svc.GetBreakdownForRangeAsync(weekStart, weekStart.AddDays(6)));
    }

    private async Task LoadMonthAsync(int year, int month)
    {
        var days = await _svc.GetMonthlyAsync(year, month);
        ViewTitle = $"月视图 · {year}-{month:00}";
        var total = days.Sum(x => x.Count);
        var sec = days.Sum(x => x.TotalSeconds);
        PomodoroKpi = total.ToString();
        DurationKpi = FormatDuration(sec);
        DailyAvgKpi = $"日均 {total / (double)days.Count:0.0}";
        ExtraKpi = $"最长连续 {await _svc.GetStreakDaysAsync(new DateTime(year, month, 1), new DateTime(year, month, days.Count))} 天";

        Series = new ISeries[]
        {
            new ColumnSeries<int>
            {
                Values = days.Select(x => x.Count).ToArray(),
                Name = "番茄",
                Fill = new SolidColorPaint(new SKColor(218, 54, 48))
            }
        };
        XAxes = new Axis[]
        {
            new()
            {
                Labels = days.Select(x => $"{x.Date:dd}").ToArray(),
                LabelsRotation = 0
            }
        };
        await FillBreakdownAsync(await _svc.GetBreakdownForRangeAsync(
            new DateTime(year, month, 1), new DateTime(year, month, days.Count)));
    }

    private async Task FillBreakdownAsync(List<TaskBreakdown> rows)
    {
        BreakdownRows.Clear();
        foreach (var r in rows) BreakdownRows.Add(r);
        await Task.CompletedTask;
    }

    private static DateTime StartOfWeek(DateTime d)
    {
        var diff = (int)d.DayOfWeek - (int)DayOfWeek.Monday;
        if (diff < 0) diff += 7;
        return d.Date.AddDays(-diff);
    }

    private static string FormatDuration(int totalSeconds)
    {
        var h = totalSeconds / 3600;
        var m = totalSeconds % 3600 / 60;
        return h > 0 ? $"{h}h {m}m" : $"{m}m";
    }
}
