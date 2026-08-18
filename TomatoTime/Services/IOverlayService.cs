namespace TomatoTime.Services;

/// <summary>管理遮罩窗显示/隐藏,按钮响应转发到 ITimerService。</summary>
public interface IOverlayService
{
    void OnStartNext();
    void OnPostpone(int minutes);
}
