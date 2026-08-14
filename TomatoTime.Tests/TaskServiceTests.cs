using TomatoTime.Data;
using TomatoTime.Services;

namespace TomatoTime.Tests;

public class TaskServiceTests
{
    private static ITaskService Create() => new TaskService(TestDb.Create());

    [Fact]
    public async Task CreateAsync_AddsTask_WithDefaults()
    {
        var svc = Create();
        var t = await svc.CreateAsync("写文档");
        Assert.Equal("写文档", t.Title);
        Assert.False(t.IsActive);
        Assert.Null(t.CompletedAt);
    }

    [Fact]
    public async Task ActivateAsync_ClearsPreviousActive_SetsNew()
    {
        var svc = Create();
        var a = await svc.CreateAsync("A");
        var b = await svc.CreateAsync("B");
        await svc.ActivateAsync(a.Id);
        await svc.ActivateAsync(b.Id);
        var active = await svc.GetActiveAsync();
        Assert.Equal(b.Id, active!.Id);
        Assert.False((await svc.GetAllAsync()).First(x => x.Id == a.Id).IsActive);
    }

    [Fact]
    public async Task CompleteAsync_SetsCompletedAt()
    {
        var svc = Create();
        var a = await svc.CreateAsync("A");
        await svc.CompleteAsync(a.Id);
        var all = await svc.GetAllAsync();
        Assert.NotNull(all.First(x => x.Id == a.Id).CompletedAt);
    }

    [Fact]
    public async Task RecordWorkSessionAsync_InsertsRow_AndCountsToday()
    {
        var svc = Create();
        var a = await svc.CreateAsync("A");
        await svc.ActivateAsync(a.Id);
        var now = DateTime.UtcNow;
        await svc.RecordWorkSessionAsync(a.Id, now.AddMinutes(-25), now, 25 * 60);
        var count = await svc.CountPomodorosTodayAsync(a.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetPomodorosCountsAsync_BatchesByTask()
    {
        var svc = Create();
        var a = await svc.CreateAsync("A");
        var b = await svc.CreateAsync("B");
        var now = DateTime.UtcNow;
        await svc.RecordWorkSessionAsync(a.Id, now.AddMinutes(-25), now, 25 * 60);
        await svc.RecordWorkSessionAsync(a.Id, now.AddMinutes(-50), now.AddMinutes(-25), 25 * 60);
        await svc.RecordWorkSessionAsync(b.Id, now.AddMinutes(-75), now.AddMinutes(-50), 25 * 60);
        var counts = await svc.GetPomodorosCountsAsync(DateTime.Today);
        Assert.Equal(2, counts[a.Id]);
        Assert.Equal(1, counts[b.Id]);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTask()
    {
        var svc = Create();
        var a = await svc.CreateAsync("A");
        await svc.DeleteAsync(a.Id);
        Assert.Empty(await svc.GetAllAsync());
    }

    [Fact]
    public async Task GetTodayPendingAsync_ExcludesCompleted()
    {
        var svc = Create();
        var a = await svc.CreateAsync("A");
        var b = await svc.CreateAsync("B");
        await svc.CompleteAsync(a.Id);
        var pending = await svc.GetTodayPendingAsync();
        Assert.Single(pending);
        Assert.Equal(b.Id, pending[0].Id);
    }
}
