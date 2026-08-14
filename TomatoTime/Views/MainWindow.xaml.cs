using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using TomatoTime.ViewModels;

namespace TomatoTime.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>"X" 按钮:隐藏到托盘而非退出(退出仅在托盘菜单)。</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized) Hide();
        base.OnStateChanged(e);
    }

    private void NewTask_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainViewModel vm && vm.Tasks.AddCommand.CanExecute(null))
            vm.Tasks.AddCommand.Execute(null);
    }
}
