namespace TomatoTime.Services;

public interface INotificationService
{
    void Notify(string title, string body);
    void StartBell();
    void StopBell();
}
