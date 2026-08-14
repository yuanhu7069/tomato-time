namespace TomatoTime.Services;

/// <summary>协调主窗 ↔ 托盘 ↔ 悬浮 ↔ 遮罩之间的显示/隐藏。</summary>
public interface IWindowService
{
    void ShowMain();
    void HideMain();
    void ToggleMain();
    void ShowFloating();
    void ShowSettings();
    void OnExit();
}
