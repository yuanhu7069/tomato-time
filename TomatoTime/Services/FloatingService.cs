using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TomatoTime.ViewModels;
using TomatoTime.Views;

namespace TomatoTime.Services;

/// <summary>悬浮窗生命周期与位置记忆。</summary>
public class FloatingService : IFloatingService
{
    private readonly IServiceProvider _sp;
    private FloatingWindow? _window;

    public FloatingService(IServiceProvider sp) => _sp = sp;

    public void Show()
    {
        _window ??= new FloatingWindow
        {
            DataContext = new FloatingViewModel(
                _sp.GetRequiredService<ITimerService>(),
                _sp.GetRequiredService<ITaskService>(),
                Application.Current.Dispatcher)
        };
        _window.Show();
    }

    public void Close()
    {
        _window?.Close();
        _window = null;
    }

    public double Left
    {
        get => _window?.Left ?? 0;
        set { if (_window != null) _window.Left = value; }
    }

    public double Top
    {
        get => _window?.Top ?? 0;
        set { if (_window != null) _window.Top = value; }
    }

    public bool IsVisible => _window?.IsVisible == true;
}
