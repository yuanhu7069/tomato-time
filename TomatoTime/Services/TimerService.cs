using Timer = System.Threading.Timer;
using TomatoTime.Models;

namespace TomatoTime.Services;

/// <summary>
/// 显式状态机驱动所有计时行为。
/// 状态:Idle → Working/Break ⇄ Paused;Working/Break 剩余=0 → Waiting;Waiting → 下一段。
/// </summary>
public class TimerService : ITimerService
{
    private readonly ISettingsService _settings;
    private readonly Func<int?> _activeLengthProvider;
    private Timer? _ticker;
    private int _postponeRemaining;

    public TimerState State { get; } = new();

    public event EventHandler<PhaseEventArgs>? PhaseEnded;
    public event EventHandler<PhaseEventArgs>? PhaseStarted;
    public event EventHandler? Tick;
    public event EventHandler? Skipped;

    public TimerService(ISettingsService settings, Func<int?>? activeLengthProvider = null)
    {
        _settings = settings;
        _activeLengthProvider = activeLengthProvider ?? (() => null);
    }

    // ---------- 测试钩子:同步走一次 Tick,避免真实定时器不确定性 ----------
    internal void TickOnce()
    {
        if (State.Status != TimerStatus.Working && State.Status != TimerStatus.Break) return;
        State.RemainingSeconds--;
        Tick?.Invoke(this, EventArgs.Empty);
        if (State.RemainingSeconds <= 0) EndPhase();
    }

    public void Start()
    {
        if (State.Status != TimerStatus.Idle) return;
        BeginPhase(PhaseKind.Work);
    }

    public void StartNext()
    {
        if (State.Status != TimerStatus.Waiting) return;
        var next = DecideNextPhase(State.Phase, State.CompletedPomodoros, _settings.LongBreakInterval);
        BeginPhase(next);
    }

    public void Pause()
    {
        if (State.Status != TimerStatus.Working && State.Status != TimerStatus.Break) return;
        _ticker?.Dispose();
        State.Status = TimerStatus.Paused;
    }

    public void Resume()
    {
        if (State.Status != TimerStatus.Paused) return;
        State.Status = State.Phase == PhaseKind.Work ? TimerStatus.Working : TimerStatus.Break;
        StartTimer();
    }

    /// <summary>跳过当前段:直接当作剩余=0 触发切换,不经过 Waiting、不弹遮罩。</summary>
    public void Skip()
    {
        if (State.Status == TimerStatus.Idle) return;
        _ticker?.Dispose();
        var justEnded = State.Phase;
        if (justEnded == PhaseKind.Work)
            State.CompletedPomodoros++; // 维持循环节奏(不写 WorkSession 流水)
        Skipped?.Invoke(this, EventArgs.Empty);
        var next = DecideNextPhase(justEnded, State.CompletedPomodoros, _settings.LongBreakInterval);
        BeginPhase(next);
    }

    /// <summary>停止:回 Idle。进行中段不计流水,已完成番茄数清零。</summary>
    public void Stop()
    {
        _ticker?.Dispose();
        State.Status = TimerStatus.Idle;
        State.RemainingSeconds = 0;
        State.CompletedPomodoros = 0;
        State.PhaseStartedAt = null;
    }

    /// <summary>稍后延迟:期到再次弹遮罩 + 响铃(不重发 Toast)。稍后期间把剩余秒同步到 State 并触发 Tick,供悬浮窗/主窗显示倒计时。</summary>
    public void Postpone(int seconds = 60)
    {
        if (State.Status != TimerStatus.Waiting) return;
        _postponeRemaining = seconds;
        State.RemainingSeconds = seconds;
        _ticker?.Dispose();
        _ticker = new Timer(_ => PostponeTickOnce(), null, 1000, 1000);
    }

    // 测试钩子:同步走一次稍后延迟
    internal void PostponeTickOnce()
    {
        _postponeRemaining--;
        State.RemainingSeconds = _postponeRemaining;
        Tick?.Invoke(this, EventArgs.Empty);
        if (_postponeRemaining <= 0)
        {
            _ticker?.Dispose();
            State.RemainingSeconds = 0;
            // 不带 PhaseStartedAt → 订阅方不会重复写 WorkSession 流水
            PhaseEnded?.Invoke(this, new PhaseEventArgs { Phase = State.Phase, RemainingSeconds = 0 });
        }
    }

    public void RestoreFrom(TimerState saved)
    {
        State.Phase = saved.Phase;
        State.Status = saved.Status;
        State.RemainingSeconds = saved.RemainingSeconds;
        State.CompletedPomodoros = saved.CompletedPomodoros;
        State.ActiveTaskId = saved.ActiveTaskId;
        if (State.Status is TimerStatus.Working or TimerStatus.Break) StartTimer();
    }

    // ---------- 内部 ----------

    private void BeginPhase(PhaseKind phase)
    {
        State.Phase = phase;
        var minutes = phase == PhaseKind.Work
            ? _activeLengthProvider() ?? _settings.WorkMinutes // 激活任务指定时长优先,否则全局
            : PhaseDuration(phase);
        State.RemainingSeconds = minutes * 60;
        State.Status = phase == PhaseKind.Work ? TimerStatus.Working : TimerStatus.Break;
        State.PhaseStartedAt = phase == PhaseKind.Work ? DateTime.UtcNow : null;
        StartTimer();
        PhaseStarted?.Invoke(this, new PhaseEventArgs { Phase = phase, RemainingSeconds = State.RemainingSeconds });
    }

    private void EndPhase()
    {
        _ticker?.Dispose();
        var endedPhase = State.Phase;
        if (endedPhase == PhaseKind.Work)
            State.CompletedPomodoros++;
        State.Status = TimerStatus.Waiting;
        State.RemainingSeconds = 0;
        PhaseEnded?.Invoke(this, new PhaseEventArgs
        {
            Phase = endedPhase,
            RemainingSeconds = 0,
            PhaseStartedAt = endedPhase == PhaseKind.Work ? State.PhaseStartedAt : null
        });
    }

    private void StartTimer()
    {
        _ticker?.Dispose();
        _ticker = new Timer(_ => TickOnce(), null, 1000, 1000);
    }

    private int PhaseDuration(PhaseKind p) => p switch
    {
        PhaseKind.Work => _settings.WorkMinutes,
        PhaseKind.ShortBreak => _settings.ShortBreakMinutes,
        PhaseKind.LongBreak => _settings.LongBreakMinutes,
        _ => _settings.WorkMinutes
    };

    /// <summary>纯函数:判定刚结束段后的下一段。</summary>
    internal static PhaseKind DecideNextPhase(PhaseKind justEnded, int completedPomodoros, int interval)
    {
        if (justEnded == PhaseKind.Work)
            return completedPomodoros % interval == 0 ? PhaseKind.LongBreak : PhaseKind.ShortBreak;
        return PhaseKind.Work; // 刚结束的是 Break → 工作
    }
}
