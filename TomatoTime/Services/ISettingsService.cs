using TomatoTime.Data.Entities;

namespace TomatoTime.Services;

public interface ISettingsService
{
    int WorkMinutes { get; }
    int ShortBreakMinutes { get; }
    int LongBreakMinutes { get; }
    int LongBreakInterval { get; }
    double OverlayOpacity { get; }
    int BellVolume { get; }
    bool RestoreOnStartup { get; }
    bool StartWithWindows { get; }

    void Reload();
    void Update(SettingsEntity updated);
}
