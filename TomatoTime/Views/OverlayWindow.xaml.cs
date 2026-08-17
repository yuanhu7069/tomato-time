using System.Windows;

namespace TomatoTime.Views;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        // 覆盖主显示器工作区,留出任务栏;实心不透明背景(保证按钮始终清晰可见)
        var wa = SystemParameters.WorkArea;
        Left = wa.Left;
        Top = wa.Top;
        Width = wa.Width;
        Height = wa.Height;
    }
}
