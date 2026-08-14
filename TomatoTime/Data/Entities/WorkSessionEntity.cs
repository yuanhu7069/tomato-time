namespace TomatoTime.Data.Entities;

public class WorkSessionEntity
{
    public int Id { get; set; }
    public int? TaskId { get; set; } // 可空:任务删除后保留流水
    public TaskEntity? Task { get; set; }
    public DateTime StartedAt { get; set; } // UTC
    public DateTime EndedAt { get; set; } // UTC
    public int DurationSeconds { get; set; }
}
