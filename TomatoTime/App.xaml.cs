using System.IO;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TomatoTime.Data;
using TomatoTime.Models;
using TomatoTime.Services;

namespace TomatoTime;

/// <summary>
/// 单进程常驻托盘:启动迁移 + 恢复计时 + 主窗 + 悬浮窗;退出仅在托盘菜单。
/// 托盘图标用代码显式创建并挂到隐藏宿主窗口,确保 Loaded 触发 Shell_NotifyIcon 注册。
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>当前托盘的 TaskbarIcon(供通知服务使用);图标挂到隐藏宿主窗口后注册。</summary>
    public static TaskbarIcon? TrayIcon { get; private set; }

    /// <summary>持有 TaskbarIcon 引用,确保其不被 GC 且已注册到托盘。</summary>
    private TaskbarIcon? _trayIcon;

    /// <summary>持有 OverlayService 引用(单例),确保其订阅 PhaseEnded 生效。</summary>
    private IOverlayService? _overlay;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            LogErrorStatic("UnhandledException", ex.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, ex) =>
        {
            LogErrorStatic("DispatcherUnhandledException", ex.Exception);
            ex.Handled = true;
        };
        Services = ServiceConfiguration.Build();

        CreateTrayIcon();

        // 关键:强制实例化 OverlayService(单例)。其构造函数订阅 TimerService.PhaseEnded,
        // 若不 Resolve,构造函数不执行 → 遮罩永远不会在段结束时弹出。
        _overlay = Services.GetRequiredService<IOverlayService>();

        MigrateDatabase();

        // 按 Settings.RestoreOnStartup 决定是否恢复上次计时
        var persist = Services.GetRequiredService<IStatePersistenceService>();
        var timer = Services.GetRequiredService<ITimerService>();
        if (persist.Load() is { } loaded)
            timer.RestoreFrom(loaded);

        var win = Services.GetRequiredService<IWindowService>();
        win.ShowMain();
        win.ShowFloating();

        // 恢复悬浮窗位置
        if (persist.LoadFloatingPosition() is { } pos)
        {
            var floating = Services.GetRequiredService<IFloatingService>();
            floating.Left = pos.x;
            floating.Top = pos.y;
        }
    }

    /// <summary>
    /// 代码创建 TaskbarIcon 并放在一个隐藏宿主窗口内。
    /// TaskbarIcon 是 FrameworkElement,图标在其 Loaded(加入可视树)时通过 Shell_NotifyIcon 注册;
    /// 仅放 XAML 资源字典不会触发 Loaded,故以前图标不出现。
    /// </summary>
    private void CreateTrayIcon()
    {
        var host = new Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            ResizeMode = ResizeMode.NoResize,
            Opacity = 0,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = int.MinValue / 2,
            Top = int.MinValue / 2
        };
        host.ShowInTaskbar = false;

        var icon = new TaskbarIcon
        {
            Icon = LoadAppIcon(),
            ToolTipText = "TomatoTime",
            ContextMenu = BuildTrayMenu()
        };
        icon.TrayLeftMouseUp += OnTrayClick;
        icon.TrayLeftMouseDoubleClick += OnTrayClick;

        // 挂到隐藏窗口内容,触发 Loaded → 注册到系统托盘
        host.Content = icon;
        _trayIcon = icon;
        TrayIcon = icon;
        _trayHost = host;
        host.Show();
        LogErrorStatic("tray", $"taskbar icon created; host visible={host.IsVisible}");
    }

    private ContextMenu BuildTrayMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(NewItem("开始", MnuStart));
        menu.Items.Add(NewItem("暂停", MnuPause));
        menu.Items.Add(NewItem("跳过", MnuSkip));
        menu.Items.Add(NewItem("设置", MnuSettings));
        menu.Items.Add(new Separator());
        menu.Items.Add(NewItem("退出", MnuExit));
        return menu;
    }

    private static MenuItem NewItem(string header, RoutedEventHandler handler)
    {
        var item = new MenuItem { Header = header };
        item.Click += handler;
        return item;
    }

    /// <summary>从嵌入资源加载托盘用 System.Drawing.Icon(多尺寸 app.ico)。</summary>
    private static System.Drawing.Icon LoadAppIcon()
    {
        var stream = Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/app.ico"))?.Stream
            ?? throw new InvalidOperationException("找不到 Assets/app.ico 资源");
        using (stream)
        {
            return new System.Drawing.Icon(stream);
        }
    }

    private Window? _trayHost;

    public static void LogErrorStatic(string kind, object? info)
    {
        try
        {
            File.AppendAllText(AppPaths.StatePath + ".error.log",
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {kind}: {info}\n");
        }
        catch
        {
            // 日志失败忽略
        }
    }

    private static void MigrateDatabase()
    {
        try
        {
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<TomatoTimeDbContext>().Database.Migrate();
        }
        catch (Exception ex)
        {
            // 兜底:备份旧库重建
            MessageBox.Show($"数据库初始化失败:\n{ex.Message}\n已备份并重建。",
                "TomatoTime", MessageBoxButton.OK, MessageBoxImage.Warning);
            var bakPath = AppPaths.DbPath + $".{DateTime.Now:HHmmss}.bak";
            if (File.Exists(AppPaths.DbPath)) File.Move(AppPaths.DbPath, bakPath);
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<TomatoTimeDbContext>().Database.Migrate();
        }
    }

    // ---------- 托盘事件 ----------

    private void OnTrayClick(object? sender, RoutedEventArgs e)
        => Services.GetRequiredService<IWindowService>().ToggleMain();

    private void MnuStart(object? sender, RoutedEventArgs e)
        => Services.GetRequiredService<ITimerService>().Start();

    private void MnuPause(object? sender, RoutedEventArgs e)
        => Services.GetRequiredService<ITimerService>().Pause();

    private void MnuSkip(object? sender, RoutedEventArgs e)
        => Services.GetRequiredService<ITimerService>().Skip();

    private void MnuSettings(object? sender, RoutedEventArgs e)
        => Services.GetRequiredService<IWindowService>().ShowSettings();

    private void MnuExit(object? sender, RoutedEventArgs e)
        => Services.GetRequiredService<IWindowService>().OnExit();

    protected override void OnExit(ExitEventArgs e)
    {
        _trayHost?.Close();
        _trayHost = null;
        _trayIcon = null;
        base.OnExit(e);
    }
}
