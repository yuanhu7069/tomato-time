using Microsoft.EntityFrameworkCore;
using TomatoTime.Data;
using TomatoTime.Data.Entities;

namespace TomatoTime.Services;

/// <summary>任务 CRUD + 激活/完成切换 + WorkSession 流水记录。</summary>
public class TaskService : ITaskService
{
    private readonly TomatoTimeDbContext _db;

    public TaskService(TomatoTimeDbContext db) => _db = db;

    public async Task<TaskEntity> CreateAsync(string title)
    {
        var maxOrder = _db.Tasks.Any() ? await _db.Tasks.MaxAsync(x => x.Order) : 0;
        var t = new TaskEntity { Title = title, CreatedAt = DateTime.UtcNow, Order = maxOrder + 1 };
        _db.Tasks.Add(t);
        await _db.SaveChangesAsync();
        return t;
    }

    public async Task<List<TaskEntity>> GetAllAsync() => await _db.Tasks.ToListAsync();

    public async Task DeleteAsync(int taskId)
    {
        var t = await _db.Tasks.FindAsync(taskId);
        if (t != null)
        {
            _db.Tasks.Remove(t);
            await _db.SaveChangesAsync();
        }
    }

    public async Task ActivateAsync(int taskId)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        var tasks = await _db.Tasks.Where(x => x.IsActive).ToListAsync();
        foreach (var x in tasks) x.IsActive = false;
        var target = await _db.Tasks.FindAsync(taskId) ?? throw new InvalidOperationException("任务不存在");
        target.IsActive = true;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<TaskEntity?> GetActiveAsync() =>
        await _db.Tasks.FirstOrDefaultAsync(x => x.IsActive);

    /// <summary>TimerService 通过此接口绑定当前任务(实际状态记录在 TimerState.ActiveTaskId)。</summary>
    public void SetActiveTaskId(int? taskId) { }

    public async Task CompleteAsync(int taskId)
    {
        var t = await _db.Tasks.FindAsync(taskId) ?? throw new InvalidOperationException("任务不存在");
        t.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private static DateTime TodayLocalStart => DateTime.Now.Date;
    private static DateTime TodayLocalEnd => TodayLocalStart.AddDays(1);

    public async Task<List<TaskEntity>> GetTodayPendingAsync() =>
        await _db.Tasks.Where(x => x.CompletedAt == null)
                       .OrderBy(x => x.Order).ThenBy(x => x.CreatedAt).ToListAsync();

    public async Task<List<TaskEntity>> GetTodayCompletedAsync()
    {
        var startUtc = TodayLocalStart.ToUniversalTime();
        var endUtc = TodayLocalEnd.ToUniversalTime();
        return await _db.Tasks.Where(x => x.CompletedAt != null
                                       && x.CompletedAt >= startUtc && x.CompletedAt < endUtc)
                       .OrderByDescending(x => x.CompletedAt).ToListAsync();
    }

    public async Task<int> CountPomodorosTodayAsync(int taskId)
    {
        var startUtc = TodayLocalStart.ToUniversalTime();
        var endUtc = TodayLocalEnd.ToUniversalTime();
        return await _db.WorkSessions.CountAsync(x => x.TaskId == taskId
                                                   && x.EndedAt >= startUtc && x.EndedAt < endUtc);
    }

    public async Task<Dictionary<int, int>> GetPomodorosCountsAsync(DateTime localDay)
    {
        var startUtc = localDay.Date.ToUniversalTime();
        var endUtc = localDay.Date.AddDays(1).ToUniversalTime();
        var rows = await _db.WorkSessions
            .Where(x => x.TaskId != null && x.EndedAt >= startUtc && x.EndedAt < endUtc)
            .Select(x => x.TaskId!.Value)
            .ToListAsync();
        return rows.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task RecordWorkSessionAsync(int? taskId, DateTime startedAt, DateTime endedAt, int durationSeconds)
    {
        _db.WorkSessions.Add(new WorkSessionEntity
        {
            TaskId = taskId,
            StartedAt = startedAt,
            EndedAt = endedAt,
            DurationSeconds = durationSeconds
        });
        await _db.SaveChangesAsync();
    }
}
