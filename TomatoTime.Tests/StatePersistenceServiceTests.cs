using System.IO;
using TomatoTime.Models;
using TomatoTime.Services;

namespace TomatoTime.Tests;

public class StatePersistenceServiceTests
{
    private sealed class FakeSettings : ISettingsService
    {
        public bool RestoreOnStartup { get; init; } = true;
        public int WorkMinutes => 25;
        public int ShortBreakMinutes => 5;
        public int LongBreakMinutes => 15;
        public int LongBreakInterval => 4;
        public double OverlayOpacity => 0.7;
        public int BellVolume => 70;
        public bool StartWithWindows => false;
        public void Reload() { }
        public void Update(TomatoTime.Data.Entities.SettingsEntity updated) { }
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"state_{Guid.NewGuid():N}.json");

    [Fact]
    public void SaveLoad_RoundTripsState()
    {
        var path = TempPath();
        var svc = new StatePersistenceService(path, new FakeSettings());
        var s = new TimerState
        {
            Phase = PhaseKind.Work,
            Status = TimerStatus.Paused,
            RemainingSeconds = 600,
            CompletedPomodoros = 2,
            ActiveTaskId = 7
        };
        svc.Save(s);
        var loaded = svc.Load();
        Assert.Equal(PhaseKind.Work, loaded!.Phase);
        Assert.Equal(TimerStatus.Paused, loaded.Status);
        Assert.Equal(600, loaded.RemainingSeconds);
        Assert.Equal(2, loaded.CompletedPomodoros);
        Assert.Equal(7, loaded.ActiveTaskId);
        File.Delete(path);
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsNull()
    {
        var svc = new StatePersistenceService("nonexistent.json", new FakeSettings());
        Assert.Null(svc.Load());
    }

    [Fact]
    public void Load_WhenRestoreDisabled_ReturnsNull()
    {
        var path = TempPath();
        var svc = new StatePersistenceService(path, new FakeSettings { RestoreOnStartup = false });
        svc.Save(new TimerState { Phase = PhaseKind.Work, Status = TimerStatus.Working, RemainingSeconds = 100 });
        Assert.Null(svc.Load());
        File.Delete(path);
    }

    [Fact]
    public void SaveFloatingPosition_ThenLoad_ReturnsPosition()
    {
        var path = TempPath();
        var svc = new StatePersistenceService(path, new FakeSettings());
        svc.SaveFloatingPosition(120.5, 88.25);
        var pos = svc.LoadFloatingPosition();
        Assert.NotNull(pos);
        Assert.Equal(120.5, pos!.Value.x);
        Assert.Equal(88.25, pos.Value.y);
        File.Delete(path);
    }

    [Fact]
    public void CombinedSave_KeepsStateAndPosition()
    {
        var path = TempPath();
        var svc = new StatePersistenceService(path, new FakeSettings());
        var s = new TimerState { Phase = PhaseKind.ShortBreak, Status = TimerStatus.Break, RemainingSeconds = 300 };
        svc.Save(s, 10, 20);
        var loaded = svc.Load();
        Assert.Equal(PhaseKind.ShortBreak, loaded!.Phase);
        Assert.Equal(300, loaded.RemainingSeconds);
        var pos = svc.LoadFloatingPosition();
        Assert.Equal((10d, 20d), pos!.Value);
        File.Delete(path);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsNull()
    {
        var path = TempPath();
        File.WriteAllText(path, "{ not valid json !!!");
        var svc = new StatePersistenceService(path, new FakeSettings());
        Assert.Null(svc.Load());
        File.Delete(path);
    }
}
