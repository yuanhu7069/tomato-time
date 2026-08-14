using TomatoTime.Models;

namespace TomatoTime.Services;

/// <summary>
/// 计时核心。持有 <see cref="TimerState"/> 单例快照,每秒 Tick,
/// 段结束时抛 <see cref="PhaseEnded"/> / <see cref="PhaseStarted"/> 事件。
/// 状态机转移逻辑全部收在本服务内,UI 与其它服务不直接判定下一段。
/// </summary>
public interface ITimerService
{
    TimerState State { get; }
    event EventHandler<PhaseEventArgs>? PhaseEnded;
    event EventHandler<PhaseEventArgs>? PhaseStarted;
    event EventHandler? Tick;
    event EventHandler? Skipped;

    void Start();
    void Pause();
    void Resume();
    void Skip();
    void Stop();
    void StartNext();
    void Postpone(int seconds = 60);
    void RestoreFrom(TimerState saved);
}
