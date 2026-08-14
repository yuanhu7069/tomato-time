using TomatoTime.Models;

namespace TomatoTime.Services;

public interface IStatePersistenceService
{
    TimerState? Load();
    void Save(TimerState state);

    /// <summary>合并保存计时状态 + 悬浮窗坐标(退出时统一写一次)。</summary>
    void Save(TimerState state, double floatX, double floatY);

    void SaveFloatingPosition(double x, double y);
    (double x, double y)? LoadFloatingPosition();
}
