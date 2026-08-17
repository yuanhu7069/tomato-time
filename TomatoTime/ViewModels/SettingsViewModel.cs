using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TomatoTime.Data.Entities;
using TomatoTime.Services;

namespace TomatoTime.ViewModels;

/// <summary>设置窗 VM:绑定各字段,保存按钮整体更新 + 开机自启注册表。</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty] private int workMinutes;
    [ObservableProperty] private int shortBreakMinutes;
    [ObservableProperty] private int longBreakMinutes;
    [ObservableProperty] private int longBreakInterval;
    [ObservableProperty] private bool startWithWindows;
    [ObservableProperty] private bool restoreOnStartup;
    [ObservableProperty] private int bellVolume;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        // 从当前缓存载入初值
        WorkMinutes = settingsService.WorkMinutes;
        ShortBreakMinutes = settingsService.ShortBreakMinutes;
        LongBreakMinutes = settingsService.LongBreakMinutes;
        LongBreakInterval = settingsService.LongBreakInterval;
        StartWithWindows = settingsService.StartWithWindows;
        RestoreOnStartup = settingsService.RestoreOnStartup;
        BellVolume = settingsService.BellVolume;
    }

    [RelayCommand]
    private void Save()
    {
        var settings = new SettingsEntity
        {
            Id = 1,
            WorkMinutes = WorkMinutes,
            ShortBreakMinutes = ShortBreakMinutes,
            LongBreakMinutes = LongBreakMinutes,
            LongBreakInterval = LongBreakInterval,
            RestoreOnStartup = RestoreOnStartup,
            StartWithWindows = StartWithWindows,
            BellVolume = BellVolume
        };
        _settingsService.Update(settings);
        ToggleStartupRegistry(StartWithWindows);
        _settingsService.Reload();
    }

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static void ToggleStartupRegistry(bool enable)
    {
        try
        {
            var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                      ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (enable)
                key.SetValue("TomatoTime", Environment.ProcessPath ?? "");
            else
                key.DeleteValue("TomatoTime", false);
        }
        catch
        {
            // 注册表写入失败不阻断设置保存
        }
    }
}
