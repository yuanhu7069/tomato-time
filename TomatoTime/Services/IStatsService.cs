using TomatoTime.Data.Entities;

namespace TomatoTime.Services;

public record DayPomodoros(DateTime Date, int Count, int TotalSeconds);

public record TaskBreakdown(int? TaskId, string Title, int Pomodoros, int TotalSeconds)
{
    /// <summary>专注时长的人类可读文本(如 1h 25m / 25m),供统计表格显示。</summary>
    public string DurationText => TotalSeconds >= 3600
        ? $"{TotalSeconds / 3600}h {TotalSeconds % 3600 / 60}m"
        : $"{TotalSeconds / 60}m";
}

public interface IStatsService
{
    Task<int> GetPomodorosForAsync(DateTime day);
    Task<int> GetTotalSecondsForAsync(DateTime day);
    Task<List<DayPomodoros>> GetDailyAsync(DateTime day);
    Task<List<DayPomodoros>> GetWeeklyAsync(DateTime weekStart);
    Task<List<DayPomodoros>> GetMonthlyAsync(int year, int month);
    Task<List<TaskBreakdown>> GetBreakdownForDayAsync(DateTime day);
    Task<List<TaskBreakdown>> GetBreakdownForRangeAsync(DateTime from, DateTime to);
    Task<int> GetStreakDaysAsync(DateTime from, DateTime to);

    /// <summary>日视图 24 小时时间线:当天每个完成的番茄按 EndedAt 的本地小时落入 0-23 桶。</summary>
    Task<int[]> GetHourlyBucketsAsync(DateTime day);
}
