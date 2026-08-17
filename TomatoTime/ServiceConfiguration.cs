using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TomatoTime.Data;
using TomatoTime.Models;
using TomatoTime.Services;

namespace TomatoTime;

/// <summary>DI 容器注册:全部服务为单例,共享同一 DbContext(SQLite 本地单用户)。</summary>
public static class ServiceConfiguration
{
    public static IServiceProvider Build()
    {
        AppPaths.EnsureDir();
        var services = new ServiceCollection();

        services.AddDbContext<TomatoTimeDbContext>(opt =>
                opt.UseSqlite($"Data Source={AppPaths.DbPath}"),
            ServiceLifetime.Singleton);

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ITaskService, TaskService>();
        services.AddSingleton<ITimerService>(sp => new TimerService(
            sp.GetRequiredService<ISettingsService>(),
            () => sp.GetRequiredService<ITaskService>().GetActivePomodoroLengthMinutes()));
        services.AddSingleton<IStatsService, StatsService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IOverlayService, OverlayService>();
        services.AddSingleton<IFloatingService, FloatingService>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<IStatePersistenceService>(sp =>
            new StatePersistenceService(AppPaths.StatePath, sp.GetRequiredService<ISettingsService>()));

        return services.BuildServiceProvider();
    }
}
