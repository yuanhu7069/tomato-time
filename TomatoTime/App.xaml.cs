using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TomatoTime.Data;
using TomatoTime.Models;
using TomatoTime.Services;

namespace TomatoTime;

/// <summary>
/// 单进程常驻托盘:启动迁移 + 恢复计时 + 主窗 + 悬浮窗;退出仅在托盘菜单。
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            LogError("UnhandledException", ex.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, ex) =>
        {
            LogError("DispatcherUnhandledException", ex.Exception);
            ex.Handled = true;
        };
        Services = ServiceConfiguration.Build();

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

    private static void LogError(string kind, object? info)
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

    private void OnTrayClick(object sender, RoutedEventArgs e)
        => Services.GetRequiredService<IWindowService>().ToggleMain();

    private void MnuStart(object sender, RoutedEventArgs e)
        => Services.GetRequiredService<ITimerService>().Start();

    private void MnuPause(object sender, RoutedEventArgs e)
        => Services.GetRequiredService<ITimerService>().Pause();

    private void MnuSkip(object sender, RoutedEventArgs e)
        => Services.GetRequiredService<ITimerService>().Skip();

    private void MnuSettings(object sender, RoutedEventArgs e)
        => Services.GetRequiredService<IWindowService>().ShowSettings();

    private void MnuExit(object sender, RoutedEventArgs e)
        => Services.GetRequiredService<IWindowService>().OnExit();
}
