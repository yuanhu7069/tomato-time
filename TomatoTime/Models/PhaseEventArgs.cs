namespace TomatoTime.Models;

/// <summary>段开始 / 结束事件参数。</summary>
public class PhaseEventArgs : EventArgs
{
    public PhaseKind Phase { get; init; }
    public int RemainingSeconds { get; init; }

    /// <summary>仅 Work 段自然归 0 时填写(用于写流水);跳过/稍后期到不填。</summary>
    public DateTime? PhaseStartedAt { get; init; }
}
