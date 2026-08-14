namespace TomatoTime.Models;

/// <summary>计时状态机的五种状态。</summary>
public enum TimerStatus
{
    Idle,
    Working,
    Break,
    Paused,
    Waiting
}
