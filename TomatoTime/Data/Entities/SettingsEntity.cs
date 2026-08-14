namespace TomatoTime.Data.Entities;

/// <summary>单行设置表,Id 固定 = 1。</summary>
public class SettingsEntity
{
    public int Id { get; set; } = 1;
    public int WorkMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int LongBreakInterval { get; set; } = 4;
    public double OverlayOpacity { get; set; } = 0.7;
    public bool RestoreOnStartup { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public int BellVolume { get; set; } = 70;
}
