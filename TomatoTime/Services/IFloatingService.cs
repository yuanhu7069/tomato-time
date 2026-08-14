namespace TomatoTime.Services;

/// <summary>管理悬浮窗生命周期与位置记忆。</summary>
public interface IFloatingService
{
    void Show();
    void Close();
    double Left { get; set; }
    double Top { get; set; }
    bool IsVisible { get; }
}
