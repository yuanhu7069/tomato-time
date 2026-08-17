using System.Windows.Media;
using TomatoTime.Services;

namespace TomatoTime.Services;

/// <summary>
/// 系统通知(托盘 BalloonTip)+ 循环响铃(嵌入 bell.wav,音量受 Settings.BellVolume 控制)。
/// Toast 失败时静默降级,仅靠响铃 + 遮罩兜底。
/// </summary>
public class NotificationService : INotificationService, IDisposable
{
    private readonly ISettingsService _settings;
    private MediaPlayer? _mp;

    public NotificationService(ISettingsService settings)
    {
        _settings = settings;
    }

    public void Notify(string title, string body)
    {
        try
        {
            var tray = TomatoTime.App.TrayIcon;
            tray?.ShowNotification(title, body);
        }
        catch
        {
            // 通知不可用:仅靠响铃 + 遮罩兜底
        }
    }

    public void StartBell()
    {
        try
        {
            _mp ??= new MediaPlayer();
            _mp.Open(new Uri("pack://application:,,,/Assets/bell.wav"));
            _mp.Volume = Math.Clamp(_settings.BellVolume, 0, 100) / 100.0;
            _mp.MediaEnded += (_, _) =>
            {
                _mp.Position = TimeSpan.Zero;
                _mp.Play();
            };
            _mp.Play();
        }
        catch
        {
            // 响铃资源缺失:降级系统提示音
            System.Media.SystemSounds.Exclamation.Play();
        }
    }

    public void StopBell() => _mp?.Stop();

    public void Dispose() => _mp?.Close();
}
