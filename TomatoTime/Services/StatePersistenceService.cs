using System.IO;
using System.Text.Json;
using TomatoTime.Models;

namespace TomatoTime.Services;

/// <summary>state.json 读写:退出时保存,启动时按 RestoreOnStartup 恢复。</summary>
public class StatePersistenceService : IStatePersistenceService
{
    private readonly string _path;
    private readonly ISettingsService _settings;

    private record PersistedState(PhaseKind Phase, TimerStatus Status, int RemainingSeconds,
                                   int CompletedPomodoros, int? ActiveTaskId,
                                   double? FloatX, double? FloatY);

    public StatePersistenceService(string path, ISettingsService settings)
    {
        _path = path;
        _settings = settings;
    }

    public TimerState? Load()
    {
        if (!_settings.RestoreOnStartup || !File.Exists(_path)) return null;
        try
        {
            var p = JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(_path));
            if (p == null) return null;
            return new TimerState
            {
                Phase = p.Phase,
                Status = p.Status,
                RemainingSeconds = p.RemainingSeconds,
                CompletedPomodoros = p.CompletedPomodoros,
                ActiveTaskId = p.ActiveTaskId
            };
        }
        catch
        {
            return null;
        }
    }

    public void Save(TimerState state) => SaveCore(state, null, null);

    public void Save(TimerState state, double floatX, double floatY) => SaveCore(state, floatX, floatY);

    private void SaveCore(TimerState state, double? floatX, double? floatY)
    {
        var p = new PersistedState(state.Phase, state.Status, state.RemainingSeconds,
                                    state.CompletedPomodoros, state.ActiveTaskId, floatX, floatY);
        File.WriteAllText(_path, JsonSerializer.Serialize(p));
    }

    public void SaveFloatingPosition(double x, double y)
    {
        var p = new PersistedState(default, default, 0, 0, null, x, y);
        File.WriteAllText(_path, JsonSerializer.Serialize(p));
    }

    public (double x, double y)? LoadFloatingPosition()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            var p = JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(_path));
            return p?.FloatX is { } x && p.FloatY is { } y && !double.IsNaN(x) && !double.IsNaN(y)
                ? (x, y) : null;
        }
        catch
        {
            return null;
        }
    }
}
