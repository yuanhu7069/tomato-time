using Microsoft.EntityFrameworkCore;
using TomatoTime.Data;
using TomatoTime.Data.Entities;

namespace TomatoTime.Services;

/// <summary>从 WorkSession 流水临时聚合日/周/月统计,不另存聚合表。</summary>
public class StatsService : IStatsService
{
    private readonly TomatoTimeDbContext _db;

    public StatsService(TomatoTimeDbContext db) => _db = db;

    private static (DateTime s, DateTime e) LocalDayRange(DateTime day)
        => (day.Date.ToUniversalTime(), day.Date.AddDays(1).ToUniversalTime());

    public async Task<int> GetPomodorosForAsync(DateTime day)
    {
        var (s, e) = LocalDayRange(day);
        return await _db.WorkSessions.CountAsync(x => x.EndedAt >= s && x.EndedAt < e);
    }

    public async Task<int> GetTotalSecondsForAsync(DateTime day)
    {
        var (s, e) = LocalDayRange(day);
        var rows = await _db.WorkSessions.Where(x => x.EndedAt >= s && x.EndedAt < e).ToListAsync();
        return rows.Sum(x => x.DurationSeconds);
    }

    public async Task<List<DayPomodoros>> GetDailyAsync(DateTime day)
    {
        var count = await GetPomodorosForAsync(day);
        var sec = await GetTotalSecondsForAsync(day);
        return new List<DayPomodoros> { new(day, count, sec) };
    }

    public async Task<List<DayPomodoros>> GetWeeklyAsync(DateTime weekStart)
    {
        var list = new List<DayPomodoros>();
        for (var i = 0; i < 7; i++)
        {
            var d = weekStart.Date.AddDays(i);
            list.Add(new(d, await GetPomodorosForAsync(d), await GetTotalSecondsForAsync(d)));
        }
        return list;
    }

    public async Task<List<DayPomodoros>> GetMonthlyAsync(int year, int month)
    {
        var days = DateTime.DaysInMonth(year, month);
        var list = new List<DayPomodoros>();
        for (var i = 1; i <= days; i++)
        {
            var d = new DateTime(year, month, i);
            list.Add(new(d, await GetPomodorosForAsync(d), await GetTotalSecondsForAsync(d)));
        }
        return list;
    }

    public async Task<List<TaskBreakdown>> GetBreakdownForDayAsync(DateTime day)
        => await GetBreakdownAsync(LocalDayRange(day));

    public async Task<List<TaskBreakdown>> GetBreakdownForRangeAsync(DateTime from, DateTime to)
        => await GetBreakdownAsync((from.Date.ToUniversalTime(), to.Date.AddDays(1).ToUniversalTime()));

    private async Task<List<TaskBreakdown>> GetBreakdownAsync((DateTime s, DateTime e) range)
    {
        var rows = await _db.WorkSessions.Where(x => x.EndedAt >= range.s && x.EndedAt < range.e).ToListAsync();
        var titles = await _db.Tasks.ToDictionaryAsync(x => x.Id, x => x.Title);
        return rows.GroupBy(r => r.TaskId)
                   .Select(g => new TaskBreakdown(g.Key,
                       g.Key is { } id && titles.TryGetValue(id, out var title) ? title : "已删除任务",
                       g.Count(), g.Sum(x => x.DurationSeconds)))
                   .OrderByDescending(x => x.Pomodoros).ToList();
    }

    public async Task<int> GetStreakDaysAsync(DateTime from, DateTime to)
    {
        var s = from.Date.ToUniversalTime();
        var e = to.Date.AddDays(1).ToUniversalTime();
        var ended = await _db.WorkSessions.Where(x => x.EndedAt >= s && x.EndedAt < e)
            .Select(x => x.EndedAt).ToListAsync();
        var set = ended.Select(d => d.ToLocalTime().Date).Distinct().ToHashSet();
        var streak = 0;
        var cur = to.Date;
        while (set.Contains(cur)) { streak++; cur = cur.AddDays(-1); }
        return streak;
    }

    public async Task<int[]> GetHourlyBucketsAsync(DateTime day)
    {
        var (s, e) = LocalDayRange(day);
        var ended = await _db.WorkSessions.Where(x => x.EndedAt >= s && x.EndedAt < e)
            .Select(x => x.EndedAt).ToListAsync();
        var buckets = new int[24];
        foreach (var d in ended) buckets[d.ToLocalTime().Hour]++;
        return buckets;
    }
}
