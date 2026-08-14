namespace TomatoTime.Models;

/// <summary>
/// 所有窗口共享的同一份计时内存快照。
/// </summary>
public class TimerState
{
    public PhaseKind Phase { get; set; }
    public TimerStatus Status { get; set; } = TimerStatus.Idle;
    public int RemainingSeconds { get; set; }
    public int CompletedPomodoros { get; set; }
    public int? ActiveTaskId { get; set; }

    /// <summary>Work 段起始时刻(UTC),用于段结束时写 WorkSession 流水。</summary>
    public DateTime? PhaseStartedAt { get; set; }
}
