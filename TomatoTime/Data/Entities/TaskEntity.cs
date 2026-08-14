namespace TomatoTime.Data.Entities;

public class TaskEntity
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } // UTC
    public bool IsActive { get; set; }
    public DateTime? CompletedAt { get; set; } // 完成时写 UTC
    public int Order { get; set; }

    public ICollection<WorkSessionEntity> WorkSessions { get; set; } = new List<WorkSessionEntity>();
}
