using TomatoTime.Models;
using TomatoTime.Services;

namespace TomatoTime.Tests;

public class TimerServiceTests
{
    private static TimerService Create() => new(new FakeSettings());

    [Fact]
    public void Initial_State_IsIdle_ZeroRemaining()
    {
        var t = Create();
        Assert.Equal(TimerStatus.Idle, t.State.Status);
        Assert.Equal(0, t.State.RemainingSeconds);
        Assert.Equal(0, t.State.CompletedPomodoros);
    }

    [Fact]
    public void Start_FromIdle_EntersWorking_WithWorkDuration()
    {
        var t = Create();
        t.Start();
        Assert.Equal(TimerStatus.Working, t.State.Status);
        Assert.Equal(PhaseKind.Work, t.State.Phase);
        Assert.Equal(25 * 60, t.State.RemainingSeconds);
        Assert.NotNull(t.State.PhaseStartedAt);
    }

    [Fact]
    public void Start_UsesActiveTaskLength_WhenProvided()
    {
        var t = new TimerService(new FakeSettings(), () => 45); // 激活任务指定 45 分钟
        t.Start();
        Assert.Equal(45 * 60, t.State.RemainingSeconds);
    }

    [Fact]
    public void WorkPhaseReachingZero_RaisesPhaseEnded_IncrementsPomodoros_EntersWaiting()
    {
        var t = Create();
        t.Start();
        PhaseKind? endedPhase = null;
        DateTime? startedAt = null;
        t.PhaseEnded += (_, e) => { endedPhase = e.Phase; startedAt = e.PhaseStartedAt; };

        t.State.RemainingSeconds = 1;
        t.TickOnce();

        Assert.Equal(PhaseKind.Work, endedPhase);
        Assert.NotNull(startedAt); // 自然结束携带起始时刻,供写流水
        Assert.Equal(TimerStatus.Waiting, t.State.Status);
        Assert.Equal(1, t.State.CompletedPomodoros);
    }

    [Theory]
    [InlineData(1, PhaseKind.ShortBreak)]
    [InlineData(2, PhaseKind.ShortBreak)]
    [InlineData(3, PhaseKind.ShortBreak)]
    [InlineData(4, PhaseKind.LongBreak)]
    public void StartNext_AfterWork_DecidesBreakByPomodoroCount(int pomodoros, PhaseKind expected)
    {
        var t = Create();
        t.Start();
        t.State.CompletedPomodoros = pomodoros; // 模拟刚做完第 n 个
        t.State.Status = TimerStatus.Waiting;
        t.State.Phase = PhaseKind.Work; // 刚结束的是 Work

        t.StartNext();
        Assert.Equal(expected, t.State.Phase);
        Assert.Equal(TimerStatus.Break, t.State.Status);
    }

    [Fact]
    public void StartNext_AfterBreak_EntersWork()
    {
        var t = Create();
        t.Start();
        t.State.Status = TimerStatus.Waiting;
        t.State.Phase = PhaseKind.ShortBreak; // 刚结束的是 Break
        t.State.CompletedPomodoros = 2;

        t.StartNext();
        Assert.Equal(PhaseKind.Work, t.State.Phase);
        Assert.Equal(TimerStatus.Working, t.State.Status);
    }

    [Theory]
    [InlineData(PhaseKind.Work, 1, 4, PhaseKind.ShortBreak)]
    [InlineData(PhaseKind.Work, 4, 4, PhaseKind.LongBreak)]
    [InlineData(PhaseKind.Work, 5, 4, PhaseKind.ShortBreak)]
    [InlineData(PhaseKind.ShortBreak, 3, 4, PhaseKind.Work)]
    [InlineData(PhaseKind.LongBreak, 0, 4, PhaseKind.Work)]
    public void DecideNextPhase_Logic(PhaseKind ended, int n, int interval, PhaseKind expected)
    {
        Assert.Equal(expected, TimerService.DecideNextPhase(ended, n, interval));
    }

    [Fact]
    public void Skip_FromWorking_RaisesSkipped_AndAdvances_WithoutWaiting()
    {
        var t = Create();
        t.Start();
        var skippedRaised = false;
        var phaseEndedRaised = false;
        t.Skipped += (_, _) => skippedRaised = true;
        t.PhaseEnded += (_, _) => phaseEndedRaised = true;

        t.Skip();

        Assert.True(skippedRaised);
        Assert.False(phaseEndedRaised); // 跳过不抛 PhaseEnded → 遮罩不弹
        Assert.NotEqual(TimerStatus.Waiting, t.State.Status);
        Assert.Equal(TimerStatus.Break, t.State.Status);
        // 跳过工作段自增番茄数以维持循环节奏,但不写流水(由调用方保证)
        Assert.Equal(1, t.State.CompletedPomodoros);
    }

    [Fact]
    public void Pause_FreezesRemaining_Resume_KeepsRemaining()
    {
        var t = Create();
        t.Start();
        t.State.RemainingSeconds = 120;
        t.Pause();
        Assert.Equal(TimerStatus.Paused, t.State.Status);
        Assert.Equal(120, t.State.RemainingSeconds);
        t.Resume();
        Assert.Equal(TimerStatus.Working, t.State.Status);
        Assert.Equal(120, t.State.RemainingSeconds);
    }

    [Fact]
    public void Stop_ResetsToIdle_AndClearsPomodoros()
    {
        var t = Create();
        t.Start();
        t.State.CompletedPomodoros = 3;
        t.State.RemainingSeconds = 100;
        t.Stop();
        Assert.Equal(TimerStatus.Idle, t.State.Status);
        Assert.Equal(0, t.State.RemainingSeconds);
        Assert.Equal(0, t.State.CompletedPomodoros);
        Assert.Null(t.State.PhaseStartedAt);
    }

    [Fact]
    public void Postpone_WhenTimerElapses_RaisesPhaseEnded_WithoutPhaseStartedAt()
    {
        var t = Create();
        t.Start();
        t.State.Status = TimerStatus.Waiting;
        PhaseEventArgs? args = null;
        t.PhaseEnded += (_, e) => args = e;

        t.Postpone(2);
        t.PostponeTickOnce();
        Assert.Null(args); // 还没到点

        t.PostponeTickOnce();
        Assert.NotNull(args);
        Assert.Null(args!.PhaseStartedAt); // 稍后期到不携带起始时刻 → 不重复写流水
    }

    [Fact]
    public void Postpone_UpdatesRemainingAndRaisesTick()
    {
        var t = Create();
        t.Start();
        t.State.Status = TimerStatus.Waiting;
        var ticks = 0;
        t.Tick += (_, _) => ticks++;

        t.Postpone(3);
        Assert.Equal(3, t.State.RemainingSeconds); // 稍后秒数同步到 State

        t.PostponeTickOnce();
        Assert.Equal(2, t.State.RemainingSeconds);
        Assert.Equal(1, ticks); // 每次稍后 Tick 触发 UI 刷新(悬浮窗倒计时更新)

        t.PostponeTickOnce();
        t.PostponeTickOnce();
        Assert.Equal(0, t.State.RemainingSeconds);
        Assert.Equal(3, ticks);
    }

    [Fact]
    public void RestoreFrom_RestoresFields_AndResumesRunningPhase()
    {
        var t = Create();
        var saved = new TimerState
        {
            Phase = PhaseKind.Work,
            Status = TimerStatus.Paused,
            RemainingSeconds = 600,
            CompletedPomodoros = 2,
            ActiveTaskId = 7
        };
        t.RestoreFrom(saved);
        Assert.Equal(PhaseKind.Work, t.State.Phase);
        Assert.Equal(TimerStatus.Paused, t.State.Status);
        Assert.Equal(600, t.State.RemainingSeconds);
        Assert.Equal(2, t.State.CompletedPomodoros);
        Assert.Equal(7, t.State.ActiveTaskId);
    }

    [Fact]
    public void Tick_DoesNothing_WhenNotRunning()
    {
        var t = Create();
        var ticks = 0;
        t.Tick += (_, _) => ticks++;
        t.TickOnce();
        Assert.Equal(0, ticks);
    }

    [Fact]
    public void BreakPhaseReachingZero_DoesNotIncrementPomodoros()
    {
        var t = Create();
        t.Start();
        t.State.CompletedPomodoros = 1; // 刚完成第 1 个番茄 → 下一段短休
        t.State.Status = TimerStatus.Waiting;
        t.StartNext();
        Assert.Equal(PhaseKind.ShortBreak, t.State.Phase);
        t.State.RemainingSeconds = 1;
        t.TickOnce();
        Assert.Equal(TimerStatus.Waiting, t.State.Status);
        Assert.Equal(1, t.State.CompletedPomodoros); // Break 结束不自增番茄数
    }

    private sealed class FakeSettings : ISettingsService
    {
        public int WorkMinutes => 25;
        public int ShortBreakMinutes => 5;
        public int LongBreakMinutes => 15;
        public int LongBreakInterval => 4;
        public double OverlayOpacity => 0.7;
        public int BellVolume => 70;
        public bool RestoreOnStartup => true;
        public bool StartWithWindows => false;
        public void Reload() { }
        public void Update(TomatoTime.Data.Entities.SettingsEntity updated) { }
    }
}
