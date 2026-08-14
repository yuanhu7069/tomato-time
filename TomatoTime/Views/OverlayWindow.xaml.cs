using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TomatoTime.Services;

namespace TomatoTime.Views;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        // 覆盖主显示器工作区,留出任务栏
        var wa = SystemParameters.WorkArea;
        Left = wa.Left;
        Top = wa.Top;
        Width = wa.Width;
        Height = wa.Height;
        Opacity = App.Services.GetRequiredService<ISettingsService>().OverlayOpacity;
    }
}
