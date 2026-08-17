namespace TomatoTime.Data.Entities;

public class TaskEntity
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } // UTC
    public bool IsActive { get; set; }
    public DateTime? CompletedAt { get; set; } // 完成时写 UTC
    public int Order { get; set; }

    /// <summary>计划完成番茄数(默认 1;用户可指定 2、3…)。</summary>
    public int PlannedPomodoros { get; set; } = 1;

    /// <summary>每个番茄的工作时长(分钟),可选 25/30/45/60;null 表示跟随全局设置。</summary>
    public int? PomodoroLengthMinutes { get; set; }

    public ICollection<WorkSessionEntity> WorkSessions { get; set; } = new List<WorkSessionEntity>();
}
