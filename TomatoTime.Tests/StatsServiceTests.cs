using TomatoTime.Data;
using TomatoTime.Data.Entities;
using TomatoTime.Services;

namespace TomatoTime.Tests;

public class StatsServiceTests
{
    [Fact]
    public async Task GetPomodorosForAsync_CountsDayRows()
    {
        var db = TestDb.Create();
        var now = DateTime.UtcNow;
        db.WorkSessions.Add(new() { TaskId = null, StartedAt = now.AddMinutes(-25), EndedAt = now, DurationSeconds = 25 * 60 });
        db.WorkSessions.Add(new() { TaskId = null, StartedAt = now.AddMinutes(-50), EndedAt = now.AddMinutes(-25), DurationSeconds = 25 * 60 });
        db.SaveChanges();
        var svc = new StatsService(db);
        Assert.Equal(2, await svc.GetPomodorosForAsync(DateTime.Today));
        Assert.Equal(2 * 25 * 60, await svc.GetTotalSecondsForAsync(DateTime.Today));
    }

    [Fact]
    public async Task GetBreakdownForDayAsync_GroupsByTask_TitleFallback()
    {
        var db = TestDb.Create();
        var a = new TaskEntity { Title = "写文档", CreatedAt = DateTime.UtcNow, Order = 1 };
        db.Tasks.Add(a);
        db.SaveChanges();
        var now = DateTime.UtcNow;
        db.WorkSessions.Add(new() { TaskId = a.Id, StartedAt = now.AddMinutes(-25), EndedAt = now, DurationSeconds = 25 * 60 });
        db.WorkSessions.Add(new() { TaskId = null, StartedAt = now.AddMinutes(-50), EndedAt = now.AddMinutes(-25), DurationSeconds = 25 * 60 });
        db.SaveChanges();
        var svc = new StatsService(db);
        var breakdown = await svc.GetBreakdownForDayAsync(DateTime.Today);
        Assert.Equal(2, breakdown.Count);
        Assert.Contains(breakdown, b => b.Title == "写文档" && b.Pomodoros == 1);
        Assert.Contains(breakdown, b => b.TaskId == null && b.Title == "已删除任务" && b.Pomodoros == 1);
    }

    [Fact]
    public async Task GetBreakdownForRangeAsync_AggregatesAcrossDays()
    {
        var db = TestDb.Create();
        var a = new TaskEntity { Title = "看书", CreatedAt = DateTime.UtcNow, Order = 1 };
        db.Tasks.Add(a);
        db.SaveChanges();
        var now = DateTime.UtcNow;
        db.WorkSessions.Add(new() { TaskId = a.Id, StartedAt = now.AddMinutes(-25), EndedAt = now, DurationSeconds = 25 * 60 });
        db.WorkSessions.Add(new() { TaskId = a.Id, StartedAt = now.AddDays(-1).AddMinutes(-25), EndedAt = now.AddDays(-1), DurationSeconds = 25 * 60 });
        db.SaveChanges();
        var svc = new StatsService(db);
        var breakdown = await svc.GetBreakdownForRangeAsync(DateTime.Today.AddDays(-7), DateTime.Today);
        var row = Assert.Single(breakdown);
        Assert.Equal("看书", row.Title);
        Assert.Equal(2, row.Pomodoros);
    }

    [Fact]
    public async Task GetStreakDaysAsync_CountsConsecutive()
    {
        var db = TestDb.Create();
        var now = DateTime.UtcNow;
        db.WorkSessions.Add(new() { TaskId = null, StartedAt = now.AddDays(-1).AddMinutes(-25), EndedAt = now.AddDays(-1), DurationSeconds = 25 * 60 });
        db.WorkSessions.Add(new() { TaskId = null, StartedAt = now.AddMinutes(-25), EndedAt = now, DurationSeconds = 25 * 60 });
        db.SaveChanges();
        var svc = new StatsService(db);
        var streak = await svc.GetStreakDaysAsync(DateTime.Today.AddDays(-7), DateTime.Today);
        Assert.Equal(2, streak);
    }

    [Fact]
    public async Task GetWeeklyAsync_ReturnsSevenDays()
    {
        var db = TestDb.Create();
        var svc = new StatsService(db);
        var week = await svc.GetWeeklyAsync(DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek));
        Assert.Equal(7, week.Count);
    }

    [Fact]
    public async Task GetMonthlyAsync_ReturnsMonthDays()
    {
        var db = TestDb.Create();
        var svc = new StatsService(db);
        var month = await svc.GetMonthlyAsync(DateTime.Today.Year, DateTime.Today.Month);
        Assert.Equal(DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month), month.Count);
    }

    [Fact]
    public async Task GetHourlyBucketsAsync_BucketsByLocalHour()
    {
        var db = TestDb.Create();
        var now = DateTime.UtcNow;
        var hour = now.ToLocalTime().Hour;
        db.WorkSessions.Add(new() { TaskId = null, StartedAt = now.AddMinutes(-25), EndedAt = now, DurationSeconds = 25 * 60 });
        db.WorkSessions.Add(new() { TaskId = null, StartedAt = now.AddMinutes(-50), EndedAt = now.AddMinutes(-25), DurationSeconds = 25 * 60 });
        db.SaveChanges();
        var svc = new StatsService(db);
        var buckets = await svc.GetHourlyBucketsAsync(DateTime.Today);
        Assert.Equal(24, buckets.Length);
        Assert.Equal(2, buckets[hour]);
    }
}
