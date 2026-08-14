using TomatoTime.Data.Entities;

namespace TomatoTime.Services;

public record DayPomodoros(DateTime Date, int Count, int TotalSeconds);

public record TaskBreakdown(int? TaskId, string Title, int Pomodoros, int TotalSeconds);

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
