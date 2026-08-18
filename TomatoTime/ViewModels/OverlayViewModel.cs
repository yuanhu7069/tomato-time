using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TomatoTime.Models;
using TomatoTime.Services;

namespace TomatoTime.ViewModels;

/// <summary>遮罩窗 VM:显示"段已结束 / 下一段名",按钮转发给 IOverlayService。</summary>
public partial class OverlayViewModel : ObservableObject
{
    private readonly ITimerService _timer;
    private readonly IOverlayService _overlay;
    private readonly ISettingsService _settings;

    [ObservableProperty] private string endedLabel = "";
    [ObservableProperty] private string nextLabel = "";
    [ObservableProperty] private string hintLabel = "";

    public OverlayViewModel(ITimerService timer, IOverlayService overlay, ISettingsService settings)
    {
        _timer = timer;
        _overlay = overlay;
        _settings = settings;
    }

    public void Configure(PhaseKind ended)
    {
        EndedLabel = ended == PhaseKind.Work ? "工作段结束 🍅" : "休息结束";
        HintLabel = ended == PhaseKind.Work ? "恭喜完成一个番茄!" : "休息好了,继续专注吧!";
        var next = TimerService.DecideNextPhase(ended, _timer.State.CompletedPomodoros, _settings.LongBreakInterval);
        NextLabel = next switch
        {
            PhaseKind.Work => "下一段:工作",
            PhaseKind.LongBreak => "下一段:长休",
            _ => "下一段:短休"
        };
    }

    [RelayCommand] private void StartNext() => _overlay.OnStartNext();
    [RelayCommand] private void Stop() => _overlay.OnStop();

    // 稍后延时三档(无参命令,避免 CommandParameter 类型转换问题)
    [RelayCommand] private void Postpone5() => _overlay.OnPostpone(5);
    [RelayCommand] private void Postpone10() => _overlay.OnPostpone(10);
    [RelayCommand] private void Postpone20() => _overlay.OnPostpone(20);
}
