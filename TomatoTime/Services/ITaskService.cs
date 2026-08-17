using TomatoTime.Data.Entities;

namespace TomatoTime.Services;

public interface ITaskService
{
    Task<TaskEntity> CreateAsync(string title);
    Task<TaskEntity> CreateAsync(string title, int? pomodoroLengthMinutes, int plannedPomodoros);
    Task UpdateTitleAsync(int taskId, string newTitle);
    Task ActivateAsync(int taskId); // 旧激活置 false,新激活置 true
    Task CompleteAsync(int taskId); // 填 CompletedAt
    Task DeleteAsync(int taskId);
    Task<List<TaskEntity>> GetTodayPendingAsync();
    Task<List<TaskEntity>> GetTodayCompletedAsync();
    Task<int> CountPomodorosTodayAsync(int taskId);

    /// <summary>同步读取当前激活任务的每番茄时长(分钟);无激活任务或未指定时返回 null(跟随全局)。</summary>
    int? GetActivePomodoroLengthMinutes();

    /// <summary>批量查询指定日期(本地)各任务的番茄数,减少 N+1 查询。</summary>
    Task<Dictionary<int, int>> GetPomodorosCountsAsync(DateTime localDay);

    Task<List<TaskEntity>> GetAllAsync();
    Task RecordWorkSessionAsync(int? taskId, DateTime startedAt, DateTime endedAt, int durationSeconds);
    Task<TaskEntity?> GetActiveAsync();
    void SetActiveTaskId(int? taskId);
}
