using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using TomatoTime.Services;

namespace TomatoTime.Views;

public partial class FloatingWindow : Window
{
    public FloatingWindow()
    {
        InitializeComponent();
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        App.Services.GetRequiredService<IWindowService>().ShowMain();
    }
}
