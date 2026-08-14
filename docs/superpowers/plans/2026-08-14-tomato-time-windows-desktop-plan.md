# TomatoTime 桌面番茄钟 实施计划

> **给执行者说明:** 用 superpowers:subagent-driven-development（每个 task 派一个子代理）或 superpowers:executing-plans 在本会话里逐 task 执行。步骤用 `- [ ]` 复选框跟踪。本计划在 Linux 写，编码与运行在 Windows 上进行。

**目标:** 在 Windows 上用 C# / WPF 构建一个常驻托盘的番茄钟桌面应用，含计时循环、任务绑定、日/周/月统计、强制提醒（通知+响铃+遮罩）、可拖动悬浮窗。

**架构:** 单进程，分 Views / ViewModels / Services / Data / Models / Assets 六层。计时由显式状态机 `ITimerService` 驱动，通过事件向各窗口广播 `Tick`/`PhaseEnded` 等。数据用 SQLite + EF Core Code-First。四个窗口共享同一份 `TimerState` 内存快照。

**技术栈:** .NET 8 / WPF / CommunityToolkit.Mvvm / Microsoft.Extensions.DependencyInjection / EF Core + Microsoft.Data.Sqlite / LiveCharts2 / xUnit（测试）。

**规格依据:** `docs/superpowers/specs/2026-08-14-tomato-time-windows-desktop-design.md`

---

## 文件结构总览

```
TomatoTime/
+- TomatoTime.csproj
+- App.xaml / App.xaml.cs
+- Views/ (MainWindow, FloatingWindow, OverlayWindow, SettingsWindow)
+- ViewModels/ (MainViewModel, TasksViewModel, StatsViewModel,
|   FloatingViewModel, OverlayViewModel, SettingsViewModel)
+- Services/ (ITimerService, ITaskService, IStatsService, ISettingsService,
|   INotificationService, IOverlayService, IFloatingService, IWindowService,
|   IStatePersistenceService + 各实现)
+- Data/ (TomatoTimeDbContext + Entities/ TaskEntity, WorkSessionEntity, SettingsEntity)
+- Models/ (TimerState, PhaseKind, TimerStatus, PhaseEventArgs, AppPaths)
+- Assets/ (bell.wav, app.ico)
+- ServiceConfiguration.cs

TomatoTime.Tests/
+- TomatoTime.Tests.csproj
+- TimerServiceTests.cs
+- TaskServiceTests.cs
+- StatsServiceTests.cs
+- StatePersistenceServiceTests.cs
+- TestDb.cs (InMemory DbContext factory helper)
```

**命名约定:** EF 实体后缀 `Entity` 避免与 `System.Threading.Tasks.Task` 冲突。ViewModel 用 CommunityToolkit.Mvvm 的 `[ObservableProperty]` / `[RelayCommand]` 源生成器。

---

## Task 1: 项目骨架与 DI

**Files:** `TomatoTime.csproj`、`TomatoTime.Tests.csproj`、`Models/AppPaths.cs`、`ServiceConfiguration.cs`、`App.xaml.cs`

- [ ] **Step 1: 创建解决方案与主项目**

```bash
dotnet new wpf -n TomatoTime -f net8.0
dotnet new sln
dotnet sln add TomatoTime/TomatoTime.csproj
```

- [ ] **Step 2: 创建测试项目并引用主项目**

```bash
dotnet new xunit -n TomatoTime.Tests -f net8.0
dotnet sln add TomatoTime.Tests/TomatoTime.Tests.csproj
dotnet add TomatoTime.Tests/TomatoTime.Tests.csproj reference TomatoTime/TomatoTime.csproj
```

- [ ] **Step 3: 添加 NuGet 依赖**

在 `TomatoTime.csproj`：

```xml
<ItemGroup>
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0" />
  <PackageReference Include="LiveChartsCore.SkiaSharpView.WPF" Version="2.0.0-rc2" />
  <PackageReference Include="H.NotifyIcon.Wpf" Version="2.1.3" />
</ItemGroup>
```

测试项目加 InMemory：

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.0" />
```

- [ ] **Step 4: 定义 AppPaths**

```csharp
namespace TomatoTime.Models;

public static class AppPaths
{
    public static string AppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TomatoTime");
    public static string DbPath => Path.Combine(AppDataDir, "tomato.db");
    public static string StatePath => Path.Combine(AppDataDir, "state.json");

    public static void EnsureDir() => Directory.CreateDirectory(AppDataDir);
}
```

- [ ] **Step 5: 写 ServiceConfiguration（DI 注册；各 Service 先用空接口+空实现占位让编译通过）**

```csharp
using Microsoft.Extensions.DependencyInjection;
using TomatoTime.Data;
using TomatoTime.Services;

namespace TomatoTime;

public static class ServiceConfiguration
{
    public static IServiceProvider Build()
    {
        Models.AppPaths.EnsureDir();
        var services = new ServiceCollection();
        services.AddDbContext<TomatoTimeDbContext>(opt =>
            opt.UseSqlite($"Data Source={Models.AppPaths.DbPath}"));
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ITimerService, TimerService>();
        services.AddSingleton<ITaskService, TaskService>();
        services.AddSingleton<IStatsService, StatsService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IOverlayService, OverlayService>();
        services.AddSingleton<IFloatingService, FloatingService>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<IStatePersistenceService, StatePersistenceService>();
        return services.BuildServiceProvider();
    }
}
```

- [ ] **Step 6: 改写 App.xaml.cs 用 DI**

```csharp
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Services = ServiceConfiguration.Build();
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
```

- [ ] **Step 7: 构建验证**

```bash
dotnet build
```
预期: BUILD SUCCEEDED（各空接口/空实现就位）。

- [ ] **Step 8: 提交**

```bash
git add -A
git commit -m "feat: 项目骨架与 DI 配置"
```

---

## Task 2: 数据层（EF Core 实体 + DbContext）

**Files:** `Data/Entities/*.cs`、`Data/TomatoTimeDbContext.cs`、`Tests/TestDb.cs`

- [ ] **Step 1: 定义 TaskEntity**

```csharp
namespace TomatoTime.Data.Entities;

public class TaskEntity
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }       // UTC
    public bool IsActive { get; set; }
    public DateTime? CompletedAt { get; set; }     // 完成时写 UTC
    public int Order { get; set; }
    public ICollection<WorkSessionEntity> WorkSessions { get; set; } = new List<WorkSessionEntity>();
}
```

- [ ] **Step 2: 定义 WorkSessionEntity**

```csharp
namespace TomatoTime.Data.Entities;

public class WorkSessionEntity
{
    public int Id { get; set; }
    public int? TaskId { get; set; }              // 可空：任务删除后保留流水
    public TaskEntity? Task { get; set; }
    public DateTime StartedAt { get; set; }       // UTC
    public DateTime EndedAt { get; set; }         // UTC
    public int DurationSeconds { get; set; }
}
```

- [ ] **Step 3: 定义 SettingsEntity（单行表，Id 固定 1）**

```csharp
namespace TomatoTime.Data.Entities;

public class SettingsEntity
{
    public int Id { get; set; } = 1;
    public int WorkMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int LongBreakInterval { get; set; } = 4;
    public double OverlayOpacity { get; set; } = 0.7;
    public bool RestoreOnStartup { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public int BellVolume { get; set; } = 70;
}
```

- [ ] **Step 4: 定义 DbContext**

```csharp
using Microsoft.EntityFrameworkCore;
using TomatoTime.Data.Entities;

namespace TomatoTime.Data;

public class TomatoTimeDbContext : DbContext
{
    public TomatoTimeDbContext(DbContextOptions<TomatoTimeDbContext> options) : base(options) { }
    public TomatoTimeDbContext() { }

    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
    public DbSet<WorkSessionEntity> WorkSessions => Set<WorkSessionEntity>();
    public DbSet<SettingsEntity> Settings => Set<SettingsEntity>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<TaskEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired();
            e.HasMany(x => x.WorkSessions).WithOne(x => x.Task!).HasForeignKey(x => x.TaskId)
             .OnDelete(DeleteBehavior.SetNull);
        });
        mb.Entity<WorkSessionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TaskId).IsRequired(false);
        });
        mb.Entity<SettingsEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasData(new SettingsEntity { Id = 1 });
        });
    }
}
```

注: `DeleteBehavior.SetNull` 保证删除任务时 WorkSession.TaskId 被置空，记录仍保留。

- [ ] **Step 5: 建测试用 InMemory DbContext factory**

```csharp
// TomatoTime.Tests/TestDb.cs
public static class TestDb
{
    public static TomatoTimeDbContext Create()
    {
        var options = new DbContextOptionsBuilder<TomatoTimeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new TomatoTimeDbContext(options);
        if (!db.Settings.Any()) db.Settings.Add(new SettingsEntity { Id = 1 });
        db.SaveChanges();
        return db;
    }
}
```

- [ ] **Step 6: 创建迁移并运行验证**

```bash
cd TomatoTime
dotnet ef migrations add InitialCreate
dotnet ef database update
```
预期: `%AppData%\TomatoTime\tomato.db` 生成，含 Tasks / WorkSessions / Settings 三张表。

- [ ] **Step 7: 提交**

```bash
git add -A
git commit -m "feat: 数据层 EF Core 实体与初始迁移"
```

---

## Task 3: 计时状态机核心（TDD）

这是全局最该被测的逻辑。所有状态转移收在 `TimerService` 里，UI/其它服务只订阅事件。

**Files:** `Models/PhaseKind.cs`、`Models/TimerStatus.cs`、`Models/TimerState.cs`、`Models/PhaseEventArgs.cs`、`Services/ITimerService.cs`、`Services/TimerService.cs`、`Services/ISettingsService.cs`（TimerService 依赖它取时长）、`Tests/TimerServiceTests.cs`

- [ ] **Step 1: 定义枚举与运行时模型**

`Models/PhaseKind.cs`:
```csharp
public enum PhaseKind { Work, ShortBreak, LongBreak }
```

`Models/TimerStatus.cs`:
```csharp
public enum TimerStatus { Idle, Working, Break, Paused, Waiting }
```

`Models/TimerState.cs`:
```csharp
public class TimerState
{
    public PhaseKind Phase { get; set; }
    public TimerStatus Status { get; set; } = TimerStatus.Idle;
    public int RemainingSeconds { get; set; }
    public int CompletedPomodoros { get; set; }
    public int? ActiveTaskId { get; set; }
    public DateTime? PhaseStartedAt { get; set; }   // Work 段起始，用于写 WorkSession
}
```

`Models/PhaseEventArgs.cs`:
```csharp
public class PhaseEventArgs : EventArgs
{
    public PhaseKind Phase { get; init; }
    public int RemainingSeconds { get; init; }
}
```

- [ ] **Step 2: 定义 ISettingsService（最小契约）**

```csharp
namespace TomatoTime.Services;

public interface ISettingsService
{
    int WorkMinutes { get; }
    int ShortBreakMinutes { get; }
    int LongBreakMinutes { get; }
    int LongBreakInterval { get; }
    double OverlayOpacity { get; }
    int BellVolume { get; }
    bool RestoreOnStartup { get; }
    void Reload();
}
```

- [ ] **Step 3: 定义 ITimerService**

```csharp
using TomatoTime.Models;

namespace TomatoTime.Services;

public interface ITimerService
{
    TimerState State { get; }
    event EventHandler<PhaseEventArgs>? PhaseEnded;
    event EventHandler<PhaseEventArgs>? PhaseStarted;
    event EventHandler? Tick;
    event EventHandler? Skipped;
    void Start();
    void Pause();
    void Resume();
    void Skip();
    void Stop();
    void StartNext();
    void Postpone(int seconds = 60);
    void RestoreFrom(TimerState saved);   // Task 11 启动恢复用
}
```

- [ ] **Step 4: 写失败测试 — 初始状态**

```csharp
using TomatoTime.Models;
using TomatoTime.Services;
using Xunit;

namespace TomatoTime.Tests;

public class TimerServiceTests
{
    private static ITimerService Create() => new TimerService(new FakeSettings());

    [Fact]
    public void Initial_State_IsIdle_ZeroRemaining()
    {
        var t = Create();
        Assert.Equal(TimerStatus.Idle, t.State.Status);
        Assert.Equal(0, t.State.RemainingSeconds);
        Assert.Equal(0, t.State.CompletedPomodoros);
    }

    private sealed class FakeSettings : ISettingsService
    {
        public int WorkMinutes => 25;
        public int ShortBreakMinutes => 5;
        public int LongBreakMinutes => 15;
        public int LongBreakInterval => 4;
        public double OverlayOpacity => 0.7;
        public int BellVolume => 70;
        public bool RestoreOnStartup => true;
        public void Reload() { }
    }
}
```

- [ ] **Step 5: 运行测试确认失败**

```bash
dotnet test
```
预期: FAIL / 编译错误（TimerService 还没实现）。

- [ ] **Step 6: 实现 TimerService 骨架让初始测试过**

```csharp
using Timer = System.Threading.Timer;
using TomatoTime.Models;

namespace TomatoTime.Services;

public class TimerService : ITimerService
{
    private readonly ISettingsService _settings;
    private Timer? _ticker;
    private int _postponeRemaining;

    public TimerState State { get; } = new();
    public event EventHandler<PhaseEventArgs>? PhaseEnded;
    public event EventHandler<PhaseEventArgs>? PhaseStarted;
    public event EventHandler? Tick;
    public event EventHandler? Skipped;

    public TimerService(ISettingsService settings) => _settings = settings;

    // 后续 step 逐个实现
    public void Start() { }
    public void Pause() { }
    public void Resume() { }
    public void Skip() { }
    public void Stop() { }
    public void StartNext() { }
    public void Postpone(int seconds = 60) { }
    public void RestoreFrom(TimerState saved) { }
}
```

- [ ] **Step 7: 测试通过后提交（骨架）**

```bash
git add -A && git commit -m "test: 计时服务初始状态测试与骨架"
```

- [ ] **Step 8: 写失败测试 — Start 从 Idle 进 Working**

```csharp
[Fact]
public void Start_FromIdle_EntersWorking_WithWorkDuration()
{
    var t = Create();
    t.Start();
    Assert.Equal(TimerStatus.Working, t.State.Status);
    Assert.Equal(PhaseKind.Work, t.State.Phase);
    Assert.Equal(25 * 60, t.State.RemainingSeconds);
    Assert.NotNull(t.State.PhaseStartedAt);
}
```

- [ ] **Step 9: 实现 Start() + BeginPhase + PhaseDuration + StartTicker**

```csharp
internal void TickOnce()
{
    if (State.Status != TimerStatus.Working && State.Status != TimerStatus.Break) return;
    State.RemainingSeconds--;
    Tick?.Invoke(this, EventArgs.Empty);
    if (State.RemainingSeconds <= 0) EndPhase();
}

private void StartTimer()
{
    _ticker?.Dispose();
    _ticker = new Timer(_ => TickOnce(), null, 1000, 1000);
}

public void Start()
{
    if (State.Status != TimerStatus.Idle) return;
    BeginPhase(PhaseKind.Work);
}

private void BeginPhase(PhaseKind phase)
{
    State.Phase = phase;
    State.RemainingSeconds = PhaseDuration(phase) * 60;
    State.Status = phase == PhaseKind.Work ? TimerStatus.Working : TimerStatus.Break;
    State.PhaseStartedAt = phase == PhaseKind.Work ? DateTime.UtcNow : null;
    StartTimer();
    PhaseStarted?.Invoke(this, new PhaseEventArgs { Phase = phase, RemainingSeconds = State.RemainingSeconds });
}

private int PhaseDuration(PhaseKind p) => p switch
{
    PhaseKind.Work => _settings.WorkMinutes,
    PhaseKind.ShortBreak => _settings.ShortBreakMinutes,
    PhaseKind.LongBreak => _settings.LongBreakMinutes,
    _ => _settings.WorkMinutes
};
```

注: 用 `internal void TickOnce()` 作测试钩子（不出真实定时器不确定性）。在项目加 `[assembly: InternalsVisibleTo("TomatoTime.Tests")]`。

- [ ] **Step 10: 写测试 — 段结束进 Waiting 并自增番茄数**

```csharp
[Fact]
public void WorkPhaseReachingZero_RaisesPhaseEnded_IncrementsPomodoros_EntersWaiting()
{
    var t = Create();
    t.Start();
    PhaseKind? endedPhase = null;
    t.PhaseEnded += (s, e) => endedPhase = e.Phase;

    t.State.RemainingSeconds = 1;
    t.TickOnce();

    Assert.Equal(PhaseKind.Work, endedPhase);
    Assert.Equal(TimerStatus.Waiting, t.State.Status);
    Assert.Equal(1, t.State.CompletedPomodoros);
}
```

- [ ] **Step 11: 实现 EndPhase()**

```csharp
private void EndPhase()
{
    _ticker?.Dispose();
    var endedPhase = State.Phase;
    if (endedPhase == PhaseKind.Work)
        State.CompletedPomodoros++;
    State.Status = TimerStatus.Waiting;
    State.RemainingSeconds = 0;
    PhaseEnded?.Invoke(this, new PhaseEventArgs { Phase = endedPhase, RemainingSeconds = 0 });
}
```

- [ ] **Step 12: 写测试 — StartNext 判定下一段**

```csharp
[Theory]
[InlineData(1, PhaseKind.ShortBreak)]
[InlineData(2, PhaseKind.ShortBreak)]
[InlineData(3, PhaseKind.ShortBreak)]
[InlineData(4, PhaseKind.LongBreak)]
public void StartNext_AfterWork_DecidesBreakByPomodoroCount(int pomodoros, PhaseKind expected)
{
    var t = Create();
    t.Start();
    t.State.CompletedPomodoros = pomodoros;   // 模拟刚做完第 n 个
    t.State.Status = TimerStatus.Waiting;
    t.State.Phase = PhaseKind.Work;           // 刚结束的是 Work

    t.StartNext();
    Assert.Equal(expected, t.State.Phase);
    Assert.True(t.State.Status == TimerStatus.Break);
}

[Fact]
public void StartNext_AfterBreak_EntersWork()
{
    var t = Create();
    t.Start();
    t.State.Status = TimerStatus.Waiting;
    t.State.Phase = PhaseKind.ShortBreak;      // 刚结束的是 Break
    t.State.CompletedPomodoros = 2;

    t.StartNext();
    Assert.Equal(PhaseKind.Work, t.State.Phase);
    Assert.Equal(TimerStatus.Working, t.State.Status);
}
```

- [ ] **Step 13: 实现 StartNext() + DecideNextPhase**

```csharp
public void StartNext()
{
    if (State.Status != TimerStatus.Waiting) return;
    var next = DecideNextPhase(State.Phase, State.CompletedPomodoros, _settings.LongBreakInterval);
    BeginPhase(next);
}

internal static PhaseKind DecideNextPhase(PhaseKind justEnded, int completedPomodoros, int interval)
{
    if (justEnded == PhaseKind.Work)
        return (completedPomodoros % interval == 0) ? PhaseKind.LongBreak : PhaseKind.ShortBreak;
    return PhaseKind.Work;   // 刚结束的是 Break → 工作
}
```

- [ ] **Step 14: 写测试 — DecideNextPhase 纯函数**

```csharp
[Theory]
[InlineData(PhaseKind.Work, 1, 4, PhaseKind.ShortBreak)]
[InlineData(PhaseKind.Work, 4, 4, PhaseKind.LongBreak)]
[InlineData(PhaseKind.Work, 5, 4, PhaseKind.ShortBreak)]
[InlineData(PhaseKind.ShortBreak, 3, 4, PhaseKind.Work)]
[InlineData(PhaseKind.LongBreak, 0, 4, PhaseKind.Work)]
public void DecideNextPhase_Logic(PhaseKind ended, int n, int interval, PhaseKind expected)
{
    Assert.Equal(expected, TimerService.DecideNextPhase(ended, n, interval));
}
```

- [ ] **Step 15: 实现 Pause / Resume / Stop / Postpone / RestoreFrom**

```csharp
public void Pause()
{
    if (State.Status != TimerStatus.Working && State.Status != TimerStatus.Break) return;
    _ticker?.Dispose();
    State.Status = TimerStatus.Paused;
}

public void Resume()
{
    if (State.Status != TimerStatus.Paused) return;
    State.Status = State.Phase == PhaseKind.Work ? TimerStatus.Working : TimerStatus.Break;
    StartTimer();
}

public void Stop()
{
    _ticker?.Dispose();
    State.Status = TimerStatus.Idle;
    State.RemainingSeconds = 0;
    State.CompletedPomodoros = 0;   // 已停止循环不复活
    State.PhaseStartedAt = null;
}

public void Postpone(int seconds = 60)
{
    if (State.Status != TimerStatus.Waiting) return;
    _postponeRemaining = seconds;
    _ticker?.Dispose();
    _ticker = new Timer(_ =>
    {
        _postponeRemaining--;
        if (_postponeRemaining <= 0)
        {
            _ticker?.Dispose();
            PhaseEnded?.Invoke(this, new PhaseEventArgs { Phase = State.Phase, RemainingSeconds = 0 });
        }
    }, null, 1000, 1000);
}

public void RestoreFrom(TimerState saved)
{
    State.Phase = saved.Phase;
    State.Status = saved.Status;
    State.RemainingSeconds = saved.RemainingSeconds;
    State.CompletedPomodoros = saved.CompletedPomodoros;
    State.ActiveTaskId = saved.ActiveTaskId;
    if (State.Status is TimerStatus.Working or TimerStatus.Break) StartTimer();
}
```

- [ ] **Step 16: 写测试 — Skip 不弹遮罩直接切下一段**

```csharp
[Fact]
public void Skip_FromWorking_RaisesSkipped_AndAdvances_WithoutWaiting()
{
    var t = Create();
    t.Start();
    bool skippedRaised = false;
    bool phaseEndedRaised = false;
    t.Skipped += (s, e) => skippedRaised = true;
    t.PhaseEnded += (s, e) => phaseEndedRaised = true;

    t.Skip();

    Assert.True(skippedRaised);
    Assert.False(phaseEndedRaised);      // 跳过不抛 PhaseEnded → 遮罩不弹
    Assert.NotEqual(TimerStatus.Waiting, t.State.Status);
    Assert.True(t.State.Status == TimerStatus.Break);
}
```

- [ ] **Step 17: 实现 Skip()**

```csharp
public void Skip()
{
    if (State.Status == TimerStatus.Idle) return;
    _ticker?.Dispose();
    var justEnded = State.Phase;
    if (justEnded == PhaseKind.Work)
        State.CompletedPomodoros++;   // 维持循环节奏（不写 WorkSession 流水）
    Skipped?.Invoke(this, EventArgs.Empty);
    var next = DecideNextPhase(justEnded, State.CompletedPomodoros, _settings.LongBreakInterval);
    BeginPhase(next);
}
```

说明: 跳过工作段不计 WorkSession 流水（没有完整跑完），但 `CompletedPomodoros` 自增以维持循环判定。这是规格里唯一留给实现者确认的口子。

- [ ] **Step 18: 全部测试跑过**

```bash
dotnet test
```
预期: 全绿。

- [ ] **Step 19: 提交**

```bash
git add -A && git commit -m "feat: 计时状态机核心实现与完整测试"
```

---

## Task 4: 任务服务（CRUD + 激活 + 完成 + 流水记录）

**Files:** `Services/ITaskService.cs`、`Services/TaskService.cs`、`Tests/TaskServiceTests.cs`

- [ ] **Step 1: 定义接口**

```csharp
using TomatoTime.Data.Entities;

namespace TomatoTime.Services;

public interface ITaskService
{
    Task<TaskEntity> CreateAsync(string title);
    Task ActivateAsync(int taskId);              // 旧激活置 false，新激活置 true
    Task CompleteAsync(int taskId);               // 填 CompletedAt
    Task DeleteAsync(int taskId);
    Task<List<TaskEntity>> GetTodayPendingAsync();
    Task<List<TaskEntity>> GetTodayCompletedAsync();
    Task<int> CountPomodorosTodayAsync(int taskId);
    Task<List<TaskEntity>> GetAllAsync();
    Task RecordWorkSessionAsync(int? taskId, DateTime startedAt, DateTime endedAt, int durationSeconds);
    Task<TaskEntity?> GetActiveAsync();
    void SetActiveTaskId(int? taskId);
}
```

- [ ] **Step 2: 写失败测试 — 创建任务**

```csharp
using TomatoTime.Data;
using TomatoTime.Data.Entities;
using TomatoTime.Services;
using Xunit;

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
}
```

- [ ] **Step 3: 实现 CreateAsync / GetAllAsync / DeleteAsync**

```csharp
using Microsoft.EntityFrameworkCore;
using TomatoTime.Data;
using TomatoTime.Data.Entities;

namespace TomatoTime.Services;

public class TaskService : ITaskService
{
    private readonly TomatoTimeDbContext _db;
    public TaskService(TomatoTimeDbContext db) => _db = db;

    public async Task<TaskEntity> CreateAsync(string title)
    {
        var maxOrder = _db.Tasks.Any() ? await _db.Tasks.MaxAsync(x => x.Order) : 0;
        var t = new TaskEntity { Title = title, CreatedAt = DateTime.UtcNow, Order = maxOrder + 1 };
        _db.Tasks.Add(t);
        await _db.SaveChangesAsync();
        return t;
    }

    public async Task<List<TaskEntity>> GetAllAsync() => await _db.Tasks.ToListAsync();

    public async Task DeleteAsync(int taskId)
    {
        var t = await _db.Tasks.FindAsync(taskId);
        if (t != null) { _db.Tasks.Remove(t); await _db.SaveChangesAsync(); }
    }
}
```

- [ ] **Step 4: 写测试 — 激活切换唯一性**

```csharp
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
```

- [ ] **Step 5: 实现 ActivateAsync / GetActiveAsync / SetActiveTaskId**

```csharp
public async Task ActivateAsync(int taskId)
{
    using var tx = await _db.Database.BeginTransactionAsync();
    var tasks = await _db.Tasks.Where(x => x.IsActive).ToListAsync();
    foreach (var x in tasks) x.IsActive = false;
    var target = await _db.Tasks.FindAsync(taskId) ?? throw new InvalidOperationException("任务不存在");
    target.IsActive = true;
    await _db.SaveChangesAsync();
    await tx.CommitAsync();
}

public async Task<TaskEntity?> GetActiveAsync() =>
    await _db.Tasks.FirstOrDefaultAsync(x => x.IsActive);

public void SetActiveTaskId(int? taskId) { }  // TimerService 通过此接口绑定当前任务
```

- [ ] **Step 6: 写测试 — 完成任务**

```csharp
[Fact]
public async Task CompleteAsync_SetsCompletedAt()
{
    var svc = Create();
    var a = await svc.CreateAsync("A");
    await svc.CompleteAsync(a.Id);
    var all = await svc.GetAllAsync();
    Assert.NotNull(all.First(x => x.Id == a.Id).CompletedAt);
}
```

- [ ] **Step 7: 实现 CompleteAsync + 今日查询 + 番茄计数**

```csharp
public async Task CompleteAsync(int taskId)
{
    var t = await _db.Tasks.FindAsync(taskId) ?? throw new InvalidOperationException("任务不存在");
    t.CompletedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync();
}

private static DateTime TodayLocalStart => DateTime.Now.Date;
private static DateTime TodayLocalEnd => TodayLocalStart.AddDays(1);

public async Task<List<TaskEntity>> GetTodayPendingAsync() =>
    await _db.Tasks.Where(x => x.CompletedAt == null)
                   .OrderBy(x => x.Order).ThenBy(x => x.CreatedAt).ToListAsync();

public async Task<List<TaskEntity>> GetTodayCompletedAsync()
{
    var startUtc = TodayLocalStart.ToUniversalTime();
    var endUtc = TodayLocalEnd.ToUniversalTime();
    return await _db.Tasks.Where(x => x.CompletedAt != null
                                  && x.CompletedAt >= startUtc && x.CompletedAt < endUtc)
                   .OrderByDescending(x => x.CompletedAt).ToListAsync();
}

public async Task<int> CountPomodorosTodayAsync(int taskId)
{
    var startUtc = TodayLocalStart.ToUniversalTime();
    var endUtc = TodayLocalEnd.ToUniversalTime();
    return await _db.WorkSessions.CountAsync(x => x.TaskId == taskId
                                               && x.EndedAt >= startUtc && x.EndedAt < endUtc);
}
```

- [ ] **Step 8: 写测试 — WorkSession 记录**

```csharp
[Fact]
public async Task RecordWorkSessionAsync_InsertsRow()
{
    var svc = Create();
    var a = await svc.CreateAsync("A");
    await svc.ActivateAsync(a.Id);
    var now = DateTime.UtcNow;
    await svc.RecordWorkSessionAsync(a.Id, now.AddMinutes(-25), now, 25 * 60);
    var count = await svc.CountPomodorosTodayAsync(a.Id);
    Assert.Equal(1, count);
}
```

- [ ] **Step 9: 实现 RecordWorkSessionAsync**

```csharp
public async Task RecordWorkSessionAsync(int? taskId, DateTime startedAt, DateTime endedAt, int durationSeconds)
{
    _db.WorkSessions.Add(new WorkSessionEntity
    {
        TaskId = taskId, StartedAt = startedAt, EndedAt = endedAt, DurationSeconds = durationSeconds
    });
    await _db.SaveChangesAsync();
}
```

- [ ] **Step 10: 跑测试 + 提交**

```bash
dotnet test
git add -A && git commit -m "feat: 任务服务 CRUD/激活/完成/流水记录"
```

---

## Task 5: 设置服务 + 状态持久化

**Files:** `Services/SettingsService.cs`、`Services/IStatePersistenceService.cs`、`Services/StatePersistenceService.cs`、`Tests/StatePersistenceServiceTests.cs`

- [ ] **Step 1: 实现 SettingsService**

```csharp
using Microsoft.EntityFrameworkCore;
using TomatoTime.Data;
using TomatoTime.Data.Entities;

namespace TomatoTime.Services;

public class SettingsService : ISettingsService
{
    private readonly TomatoTimeDbContext _db;
    private SettingsEntity _cache;

    public SettingsService(TomatoTimeDbContext db)
    {
        _db = db;
        if (!_db.Settings.Any()) _db.Settings.Add(new SettingsEntity { Id = 1 });
        _db.SaveChanges();
        _cache = _db.Settings.Single(x => x.Id == 1);
    }

    public int WorkMinutes => _cache.WorkMinutes;
    public int ShortBreakMinutes => _cache.ShortBreakMinutes;
    public int LongBreakMinutes => _cache.LongBreakMinutes;
    public int LongBreakInterval => _cache.LongBreakInterval;
    public double OverlayOpacity => _cache.OverlayOpacity;
    public int BellVolume => _cache.BellVolume;
    public bool RestoreOnStartup => _cache.RestoreOnStartup;

    public void Reload() { _cache = _db.Settings.Single(x => x.Id == 1); _db.Entry(_cache).Reload(); }

    public void Update(SettingsEntity updated)
    {
        _cache = updated;
        _db.Settings.Update(updated);
        _db.SaveChanges();
    }
}
```

- [ ] **Step 2: 定义 IStatePersistenceService**

```csharp
using TomatoTime.Models;

namespace TomatoTime.Services;

public interface IStatePersistenceService
{
    TimerState? Load();
    void Save(TimerState state);
    void SaveFloatingPosition(double x, double y);
    (double x, double y)? LoadFloatingPosition();
}
```

- [ ] **Step 3: 写失败测试 — 往返状态**

```csharp
using System.Text.Json;
using TomatoTime.Models;
using TomatoTime.Services;
using Xunit;

namespace TomatoTime.Tests;

public class StatePersistenceServiceTests
{
    private sealed class FakeSettings : ISettingsService
    {
        public int WorkMinutes => 25;
        public int ShortBreakMinutes => 5;
        public int LongBreakMinutes => 15;
        public int LongBreakInterval => 4;
        public double OverlayOpacity => 0.7;
        public int BellVolume => 70;
        public bool RestoreOnStartup => true;
        public void Reload() { }
    }

    [Fact]
    public void SaveLoad_RoundTripsState()
    {
        var path = Path.Combine(Path.GetTempPath(), $"state_{Guid.NewGuid():N}.json");
        var svc = new StatePersistenceService(path, new FakeSettings());
        var s = new TimerState { Phase = PhaseKind.Work, Status = TimerStatus.Paused,
                                 RemainingSeconds = 600, CompletedPomodoros = 2, ActiveTaskId = 7 };
        svc.Save(s);
        var loaded = svc.Load();
        Assert.Equal(PhaseKind.Work, loaded!.Phase);
        Assert.Equal(TimerStatus.Paused, loaded.Status);
        Assert.Equal(600, loaded.RemainingSeconds);
        Assert.Equal(2, loaded.CompletedPomodoros);
        Assert.Equal(7, loaded.ActiveTaskId);
        File.Delete(path);
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsNull()
    {
        var svc = new StatePersistenceService("nonexistent.json", new FakeSettings());
        Assert.Null(svc.Load());
    }
}
```

- [ ] **Step 4: 实现 StatePersistenceService**

```csharp
using System.Text.Json;
using TomatoTime.Models;

namespace TomatoTime.Services;

public class StatePersistenceService : IStatePersistenceService
{
    private readonly string _path;
    private readonly ISettingsService _settings;

    private record PersistedState(PhaseKind Phase, TimerStatus Status, int RemainingSeconds,
                                   int CompletedPomodoros, int? ActiveTaskId,
                                   double? FloatX, double? FloatY);

    public StatePersistenceService(string path, ISettingsService settings)
    { _path = path; _settings = settings; }

    public TimerState? Load()
    {
        if (!_settings.RestoreOnStartup || !File.Exists(_path)) return null;
        try
        {
            var p = JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(_path));
            if (p == null) return null;
            return new TimerState { Phase = p.Phase, Status = p.Status,
                RemainingSeconds = p.RemainingSeconds, CompletedPomodoros = p.CompletedPomodoros,
                ActiveTaskId = p.ActiveTaskId };
        }
        catch { return null; }
    }

    public void Save(TimerState state)
    {
        var p = new PersistedState(state.Phase, state.Status, state.RemainingSeconds,
                                    state.CompletedPomodoros, state.ActiveTaskId, null, null);
        File.WriteAllText(_path, JsonSerializer.Serialize(p));
    }

    public void SaveFloatingPosition(double x, double y)
    {
        var p = new PersistedState(default, default, 0, 0, null, x, y);
        File.WriteAllText(_path, JsonSerializer.Serialize(p));
    }

    public (double x, double y)? LoadFloatingPosition()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            var p = JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(_path));
            return (p?.FloatX, p?.FloatY) is ({ } x, { } y) ? (x, y) : null;
        }
        catch { return null; }
    }
}
```

注: `SaveFloatingPosition` 的真实实现应合并写整份 state.json（含计时 + 悬浮坐标），上面是简化桩，Task 11 Step 6 统一完善。

- [ ] **Step 5: 测试 + 提交**

```bash
dotnet test
git add -A && git commit -m "feat: 设置服务与状态持久化"
```

---

## Task 6: 主窗 UI

这块是 WPF XAML，给结构 + 绑定契约，不逐像素写样式。

**Files:** `Views/MainWindow.xaml` / `.cs`、`ViewModels/MainViewModel.cs`、`ViewModels/TasksViewModel.cs`

- [ ] **Step 1: MainViewModel — 计时视图绑定**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TomatoTime.Models;
using TomatoTime.Services;

namespace TomatoTime.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ITimerService _timer;
    private readonly Dispatcher _dispatcher;

    [ObservableProperty] private string remainingText = "25:00";
    [ObservableProperty] private string phaseLabel = "工作";
    [ObservableProperty] private string currentTaskTitle = "(未选择任务)";

    public TasksViewModel Tasks { get; }
    public StatsViewModel Stats { get; }

    public MainViewModel(ITimerService timer, ITaskService tasks, IStatsService stats, Dispatcher dispatcher)
    {
        _timer = timer; _dispatcher = dispatcher;
        Tasks = new TasksViewModel(tasks, dispatcher);
        Stats = new StatsViewModel(stats);
        _timer.Tick += (_, _) => _dispatcher.BeginInvoke(RefreshDisplay);
        _timer.PhaseStarted += (_, e) => _dispatcher.BeginInvoke(() => PhaseLabel = LabelFor(e.Phase));
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        var r = _timer.State.RemainingSeconds;
        RemainingText = $"{r / 60:00}:{r % 60:00}";
    }

    private static string LabelFor(PhaseKind p) => p switch
    { PhaseKind.Work => "工作", PhaseKind.ShortBreak => "短休", _ => "长休" };

    [RelayCommand] private void Start() => _timer.Start();
    [RelayCommand] private void Pause() => _timer.Pause();
    [RelayCommand] private void Resume() => _timer.Resume();
    [RelayCommand] private void Skip() => _timer.Skip();
    [RelayCommand] private void Stop() => _timer.Stop();
}
```

- [ ] **Step 2: TasksViewModel — 今日待办/已完成**

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TomatoTime.Data.Entities;
using TomatoTime.Services;

namespace TomatoTime.ViewModels;

public partial class TasksViewModel : ObservableObject
{
    private readonly ITaskService _svc;
    private readonly Dispatcher _dispatcher;
    private DateTime _lastRefreshDate = DateTime.MinValue;

    [ObservableProperty] private bool showCompleted;
    [ObservableProperty] private int completedCount;
    [ObservableProperty] private string newTaskTitle = "";

    public ObservableCollection<TaskRow> Pending { get; } = new();
    public ObservableCollection<TaskRow> Completed { get; } = new();

    public TasksViewModel(ITaskService svc, Dispatcher d)
    { _svc = svc; _dispatcher = d; _ = RefreshAsync(); }

    public record TaskRow(int Id, string Title, int TodayPomodoros, bool IsActive);
    public TaskRow(TaskEntity e, int todayCount) => new TaskRow(e.Id, e.Title, todayCount, e.IsActive);

    public async Task RefreshAsync()
    {
        _lastRefreshDate = DateTime.Today;
        var pending = await _svc.GetTodayPendingAsync();
        var done = await _svc.GetTodayCompletedAsync();
        _dispatcher.BeginInvoke(() =>
        {
            Pending.Clear();
            foreach (var t in pending)
                Pending.Add(new TaskRow(t.Id, t.Title, _svc.CountPomodorosTodayAsync(t.Id).Result, t.IsActive));
            Completed.Clear();
            foreach (var t in done)
                Completed.Add(new TaskRow(t.Id, t.Title, _svc.CountPomodorosTodayAsync(t.Id).Result, t.IsActive));
            CompletedCount = Completed.Count;
        });
    }

    [RelayCommand]
    private async Task Add()
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle)) return;
        await _svc.CreateAsync(NewTaskTitle);
        NewTaskTitle = "";
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ToggleComplete(TaskRow row) { await _svc.CompleteAsync(row.Id); await RefreshAsync(); }

    [RelayCommand]
    private async Task Activate(TaskRow row) { await _svc.ActivateAsync(row.Id); await RefreshAsync(); }
}
```

说明: `CountPomodorosTodayAsync` 应批量一次查回再构造 row，避免 N 次查询；这里给结构，优化在 Task 12。

- [ ] **Step 3: MainWindow.xaml 结构**

```xml
<Window x:Class="TomatoTime.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="TomatoTime" Height="600" Width="420"
        WindowStartupLocation="CenterScreen">
  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <!-- 计时视图 -->
    <StackPanel Grid.Row="0" HorizontalAlignment="Center" Margin="0,20">
      <TextBlock Text="{Binding PhaseLabel}" FontSize="18" HorizontalAlignment="Center" Opacity="0.7"/>
      <TextBlock Text="{Binding RemainingText}" FontSize="64" HorizontalAlignment="Center"/>
      <TextBlock Text="{Binding CurrentTaskTitle}" FontSize="13" HorizontalAlignment="Center" Margin="0,6"/>
      <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,12">
        <Button Content="开始" Command="{Binding StartCommand}" Margin="4"/>
        <Button Content="暂停" Command="{Binding PauseCommand}" Margin="4"/>
        <Button Content="跳过" Command="{Binding SkipCommand}" Margin="4"/>
        <Button Content="停止" Command="{Binding StopCommand}" Margin="4"/>
      </StackPanel>
    </StackPanel>

    <!-- 任务面板 -->
    <Grid Grid.Row="1" Margin="10">
      <Grid.RowDefinitions>
        <RowDefinition Height="*"/>
        <RowDefinition Height="Auto"/>
      </Grid.RowDefinitions>
      <DockPanel Grid.Row="0">
        <TextBlock Text="今日待办" DockPanel.Dock="Top" FontWeight="Bold" Margin="0,0,0,6"/>
        <ScrollViewer>
          <ItemsControl ItemsSource="{Binding Tasks.Pending}">
            <ItemsControl.ItemTemplate>
              <DataTemplate>
                <DockPanel Margin="0,3">
                  <CheckBox IsChecked="{Binding IsActive}" Command="{Binding DataContext.Tasks.ActivateCommand, RelativeSource={RelativeSource AncestorType=Window}}" CommandParameter="{Binding}"/>
                  <TextBlock Text="{Binding Title}" Margin="8,0"/>
                  <TextBlock Text="{Binding TodayPomodoros}" DockPanel.Dock="Right" Opacity="0.6"/>
                </DockPanel>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
        </ScrollViewer>
      </DockPanel>
      <Expander Grid.Row="1" Header="{Binding CompletedCount, StringFormat='今日已完成 ({0})'}"
                IsExpanded="{Binding ShowCompleted}">
        <ListBox ItemsSource="{Binding Tasks.Completed}"/>
      </Expander>
    </Grid>

    <!-- Tab：今日待办 | 统计 -->
    <TabControl Grid.Row="2">
      <TabItem Header="今日待办" IsSelected="True"/>
      <TabItem Header="统计">
        <ContentControl Content="{Binding Stats}"/>
      </TabItem>
    </TabControl>
  </Grid>
</Window>
```

- [ ] **Step 4: MainWindow.xaml.cs 装配 ViewModel**

```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var sp = ((App)Application.Current).Services;
        DataContext = new MainViewModel(
            sp.GetRequiredService<ITimerService>(),
            sp.GetRequiredService<ITaskService>(),
            sp.GetRequiredService<IStatsService>(),
            Application.Current.Dispatcher);
    }
}
```

- [ ] **Step 5: 编译验证 + 提交**

```bash
dotnet build
git add -A && git commit -m "feat: 主窗计时视图与任务面板"
```

---

## Task 7: 悬浮窗

**Files:** `Views/FloatingWindow.xaml` / `.cs`、`ViewModels/FloatingViewModel.cs`

- [ ] **Step 1: FloatingViewModel**

```csharp
public partial class FloatingViewModel : ObservableObject
{
    private readonly ITimerService _timer;
    private readonly Dispatcher _dispatcher;
    [ObservableProperty] private string remainingText = "25:00";
    [ObservableProperty] private string taskTitle = "";
    [ObservableProperty] private bool isRunning;

    public FloatingViewModel(ITimerService timer, Dispatcher d)
    {
        _timer = timer; _dispatcher = d;
        _timer.Tick += (_, _) => _dispatcher.BeginInvoke(Refresh);
        Refresh();
    }

    private void Refresh()
    {
        var r = _timer.State.RemainingSeconds;
        RemainingText = $"{r / 60:00}:{r % 60:00}";
        IsRunning = _timer.State.Status is TimerStatus.Working or TimerStatus.Break;
    }

    [RelayCommand] private void Expand()
    {
        App.Services.GetRequiredService<IWindowService>().ShowMain();
    }
}
```

注: TaskTitle 应从 ITaskService 取当前激活任务标题，不是 ID。

- [ ] **Step 2: FloatingWindow.xaml — Topmost 可拖动小窗**

```xml
<Window x:Class="TomatoTime.Views.FloatingWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Width="160" Height="56" WindowStyle="None" AllowsTransparency="True"
        Background="#E6202020" Topmost="True" ShowInTaskbar="False"
        ResizeMode="NoResize" MouseLeftButtonDown="OnDrag"
        MouseDoubleClick="OnDoubleClick">
  <DockPanel>
    <TextBlock Text="▶" DockPanel.Dock="Left" Foreground="#E05858" FontSize="14"
               VerticalAlignment="Center" Margin="8,0"/>
    <TextBlock Text="{Binding RemainingText}" Foreground="White" FontSize="18" VerticalAlignment="Center"/>
    <TextBlock Text="{Binding TaskTitle}" Foreground="#9ab" FontSize="10"
               VerticalAlignment="Center" Margin="4,0,8,0"
               TextTrimming="CharacterEllipsis" DockPanel.Dock="Right"/>
  </DockPanel>
</Window>
```

- [ ] **Step 3: 拖动 + 双击展开**

```csharp
public partial class FloatingWindow : Window
{
    public FloatingWindow() { InitializeComponent(); }

    private void OnDrag(object sender, MouseButtonEventArgs e) { DragMove(); }

    private void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        App.Services.GetRequiredService<IWindowService>().ShowMain();
    }
}
```

- [ ] **Step 4: 编译验证 + 提交**

```bash
dotnet build
git add -A && git commit -m "feat: 悬浮窗倒计时与拖动"
```

---

## Task 8: 遮罩窗 + 提醒流程

**Files:** `Views/OverlayWindow.xaml` / `.cs`、`ViewModels/OverlayViewModel.cs`、`Services/IOverlayService.cs` / `OverlayService.cs`、`Services/INotificationService.cs` / `NotificationService.cs`

- [ ] **Step 1: 定义 INotificationService 并实现**

```csharp
namespace TomatoTime.Services;

public interface INotificationService
{
    void Notify(string title, string body);
    void StartBell();
    void StopBell();
}
```

```csharp
using System.Windows.Media;

public class NotificationService : INotificationService, IDisposable
{
    private readonly ISettingsService _settings;
    private MediaPlayer? _mp;
    private System.Threading.Timer? _bellTimer;

    public NotificationService(ISettingsService s) => _settings = s;

    public void Notify(string title, string body)
    {
        // 优先 Toast（CommunityToolkit.WinUI.Notifications），降级 H.NotifyIcon BalloonTip
        // 首次启动若系统通知被禁用 → 仅靠响铃+遮罩兜底
    }

    public void StartBell()
    {
        _mp ??= new MediaPlayer();
        _mp.Open(new Uri("pack://application:,,,/Assets/bell.wav"));
        _mp.Volume = _settings.BellVolume / 100.0;
        _mp.MediaEnded += (s, e) => _mp.Position = TimeSpan.Zero;  // 循环
        _mp.Play();
    }

    public void StopBell()
    {
        _mp?.Stop();
    }

    public void Dispose() => _mp?.Close();
}
```

- [ ] **Step 2: OverlayViewModel**

```csharp
public partial class OverlayViewModel : ObservableObject
{
    private readonly ITimerService _timer;
    private readonly IOverlayService _overlay;
    private readonly ISettingsService _settings;

    [ObservableProperty] private string endedLabel = "";
    [ObservableProperty] private string nextLabel = "";

    public OverlayViewModel(ITimerService timer, IOverlayService overlay, ISettingsService settings)
    { _timer = timer; _overlay = overlay; _settings = settings; }

    public void Configure(PhaseKind ended)
    {
        EndedLabel = ended == PhaseKind.Work ? "工作段结束" : "休息结束";
        var next = TimerService.DecideNextPhase(ended, _timer.State.CompletedPomodoros, _settings.LongBreakInterval);
        NextLabel = next == PhaseKind.Work ? "下一段：工作"
                  : next == PhaseKind.LongBreak ? "下一段：长休" : "下一段：短休";
    }

    [RelayCommand] private void StartNext() => _overlay.OnStartNext();
    [RelayCommand] private void Postpone() => _overlay.OnPostpone();
}
```

- [ ] **Step 3: OverlayWindow.xaml — Topmost 满屏半透明**

```xml
<Window x:Class="TomatoTime.Views.OverlayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None" AllowsTransparency="True" Topmost="True"
        ShowInTaskbar="False" WindowState="Maximized">
  <Grid Background="#B3000000">
    <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center">
      <TextBlock Text="{Binding EndedLabel}" Foreground="White" FontSize="48" HorizontalAlignment="Center"/>
      <TextBlock Text="{Binding NextLabel}" Foreground="#ccc" FontSize="24" Margin="0,12" HorizontalAlignment="Center"/>
      <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,24">
        <Button Content="开始下一段" Command="{Binding StartNextCommand}" Padding="20,12" Margin="8"/>
        <Button Content="稍后 1 分" Command="{Binding PostponeCommand}" Padding="20,12" Margin="8"/>
      </StackPanel>
    </StackPanel>
  </Grid>
</Window>
```

- [ ] **Step 4: 遮罩覆盖工作区留任务栏 + 不透明度绑定**

```csharp
public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        var wa = SystemParameters.WorkArea;   // 排除任务栏
        Left = wa.Left; Top = wa.Top; Width = wa.Width; Height = wa.Height;
        Opacity = ((ISettingsService)App.Services.GetService(typeof(ISettingsService))!).OverlayOpacity;
    }
}
```

- [ ] **Step 5: IOverlayService — 订阅 PhaseEnded 弹遮罩**

```csharp
public interface IOverlayService
{
    void OnStartNext();
    void OnPostpone();
}

public class OverlayService : IOverlayService
{
    private readonly ITimerService _timer;
    private readonly INotificationService _notify;
    private readonly ISettingsService _settings;
    private OverlayWindow? _window;

    public OverlayService(ITimerService t, INotificationService n, ISettingsService s, IServiceProvider sp)
    {
        _timer = t; _notify = n; _settings = s;
        _timer.PhaseEnded += (_, e) => Application.Current.Dispatcher.BeginInvoke(() => Show(e));
    }

    private void Show(PhaseEventArgs e)
    {
        _notify.Notify("TomatoTime", e.Phase == PhaseKind.Work ? "工作段结束！" : "休息结束！");
        _notify.StartBell();
        _window = new OverlayWindow();
        var vm = new OverlayViewModel(_timer, this, _settings);
        vm.Configure(e.Phase);
        _window.DataContext = vm;
        _window.Show();
    }

    public void OnStartNext() { _notify.StopBell(); _window?.Close(); _timer.StartNext(); }
    public void OnPostpone()  { _notify.StopBell(); _window?.Close(); _timer.Postpone(); }
}
```

注: Skipped 事件不被 OverlayService 订阅，跳过时 TimerService 只抛 `Skipped`、不抛 `PhaseEnded`，遮罩自然不弹（由 Task 3 Step 16 测试覆盖）。

- [ ] **Step 6: WorkSession 流水写入接入**

在 TimerService 的 EndPhase() 里，当 `endedPhase == PhaseKind.Work` 时需调 `ITaskService.RecordWorkSessionAsync`。TimerService 应通过事件回调让外层（IOverlayService 或专门的 IWorkSessionRecorder）写库，而非直接依赖 DbContext。方案: 在 PhaseEnded 事件参数里加 `PhaseStartedAt`，由订阅方算 StartedAt/EndedAt 并写库。修改 PhaseEventArgs:

```csharp
public class PhaseEventArgs : EventArgs
{
    public PhaseKind Phase { get; init; }
    public int RemainingSeconds { get; init; }
    public DateTime? PhaseStartedAt { get; init; }   // 仅 Work 段填
}
```

EndPhase() 里 `PhaseEnded?.Invoke(this, new PhaseEventArgs { Phase = endedPhase, RemainingSeconds = 0, PhaseStartedAt = State.PhaseStartedAt })`。

由 IOverlayService.Show 的订阅回调或独立 `IWorkSessionRecorder` 处理:
```csharp
if (e.Phase == PhaseKind.Work && e.PhaseStartedAt is { } started)
{
    var now = DateTime.UtcNow;
    var activeTask = await _tasks.GetActiveAsync();
    await _tasks.RecordWorkSessionAsync(activeTask?.Id, started, now,
        (int)(now - started).TotalSeconds);
}
```

- [ ] **Step 7: 编译验证 + 提交**

```bash
dotnet build
git add -A && git commit -m "feat: 遮罩窗与提醒流程"
```

---

## Task 9: 统计页 + IStatsService

**Files:** `Services/IStatsService.cs` / `StatsService.cs`、`ViewModels/StatsViewModel.cs`、`MainWindow.xaml` 内统计 Tab 扩展、`Tests/StatsServiceTests.cs`

- [ ] **Step 1: 定义 IStatsService 与 DTO**

```csharp
using TomatoTime.Data.Entities;

namespace TomatoTime.Services;

public record DayPomodoros(DateTime Date, int Count, int TotalSeconds);
public record TaskBreakdown(int? TaskId, string Title, int Pomodoros, int TotalSeconds);

public interface IStatsService
{
    Task<int> GetPomodorosForAsync(DateTime day);
    Task<int> GetTotalSecondsForAsync(DateTime day);
    Task<List<DayPomodoros>> GetDailyAsync(DateTime day);
    Task<List<DayPomodoros>> GetWeeklyAsync(DateTime weekStart);
    Task<List<DayPomodoros>> GetMonthlyAsync(int year, int month);
    Task<List<TaskBreakdown>> GetBreakdownForDayAsync(DateTime day);
    Task<List<TaskBreakdown>> GetBreakdownForRangeAsync(DateTime from, DateTime to);
    Task<int> GetStreakDaysAsync(DateTime from, DateTime to);
}
```

- [ ] **Step 2: 实现 StatsService 聚合查询**

```csharp
using Microsoft.EntityFrameworkCore;
using TomatoTime.Data;
using TomatoTime.Data.Entities;

namespace TomatoTime.Services;

public class StatsService : IStatsService
{
    private readonly TomatoTimeDbContext _db;
    public StatsService(TomatoTimeDbContext db) => _db = db;

    private static (DateTime s, DateTime e) LocalDayRange(DateTime day)
        => (day.Date.ToUniversalTime(), day.Date.AddDays(1).ToUniversalTime());

    public async Task<int> GetPomodorosForAsync(DateTime day)
    {
        var (s, e) = LocalDayRange(day);
        return await _db.WorkSessions.CountAsync(x => x.EndedAt >= s && x.EndedAt < e);
    }

    public async Task<int> GetTotalSecondsForAsync(DateTime day)
    {
        var (s, e) = LocalDayRange(day);
        var rows = await _db.WorkSessions.Where(x => x.EndedAt >= s && x.EndedAt < e).ToListAsync();
        return rows.Sum(x => x.DurationSeconds);
    }

    public async Task<List<DayPomodoros>> GetDailyAsync(DateTime day)
    {
        var count = await GetPomodorosForAsync(day);
        var sec = await GetTotalSecondsForAsync(day);
        return new List<DayPomodoros> { new(day, count, sec) };
    }

    public async Task<List<DayPomodoros>> GetWeeklyAsync(DateTime weekStart)
    {
        var list = new List<DayPomodoros>();
        for (int i = 0; i < 7; i++)
        {
            var d = weekStart.Date.AddDays(i);
            list.Add(new(d, await GetPomodorosForAsync(d), await GetTotalSecondsForAsync(d)));
        }
        return list;
    }

    public async Task<List<DayPomodoros>> GetMonthlyAsync(int year, int month)
    {
        var days = DateTime.DaysInMonth(year, month);
        var list = new List<DayPomodoros>();
        for (int i = 1; i <= days; i++)
        {
            var d = new DateTime(year, month, i);
            list.Add(new(d, await GetPomodorosForAsync(d), await GetTotalSecondsForAsync(d)));
        }
        return list;
    }

    public async Task<List<TaskBreakdown>> GetBreakdownForDayAsync(DateTime day)
    {
        var (s, e) = LocalDayRange(day);
        var rows = await _db.WorkSessions
            .Where(x => x.EndedAt >= s && x.EndedAt < e)
            .Join(_db.Tasks, ws => ws.TaskId, t => t.Id, (ws, t) => new { ws, t })
            .ToListAsync();
        return rows.GroupBy(r => r.ws.TaskId)
                   .Select(g => new TaskBreakdown(g.Key,
                        g.FirstOrDefault()?.t?.Title ?? "已删除任务",
                        g.Count(), g.Sum(x => x.ws.DurationSeconds)))
                   .OrderByDescending(x => x.Pomodoros).ToList();
    }

    public async Task<List<TaskBreakdown>> GetBreakdownForRangeAsync(DateTime from, DateTime to)
    {
        var s = from.Date.ToUniversalTime();
        var e = to.Date.AddDays(1).ToUniversalTime();
        var rows = await _db.WorkSessions
            .Where(x => x.EndedAt >= s && x.EndedAt < e)
            .Join(_db.Tasks, ws => ws.TaskId, t => t.Id, (ws, t) => new { ws, t })
            .ToListAsync();
        return rows.GroupBy(r => r.ws.TaskId)
                   .Select(g => new TaskBreakdown(g.Key,
                        g.FirstOrDefault()?.t?.Title ?? "已删除任务",
                        g.Count(), g.Sum(x => x.ws.DurationSeconds)))
                   .OrderByDescending(x => x.Pomodoros).ToList();
    }

    public async Task<int> GetStreakDaysAsync(DateTime from, DateTime to)
    {
        var s = from.Date.ToUniversalTime();
        var e = to.Date.AddDays(1).ToUniversalTime();
        var days = await _db.WorkSessions.Where(x => x.EndedAt >= s && x.EndedAt < e)
            .Select(x => x.EndedAt).ToListAsync();
        var set = days.Select(d => d.ToLocalTime().Date).Distinct().ToHashSet();
        int streak = 0;
        var cur = to.Date;
        while (set.Contains(cur)) { streak++; cur = cur.AddDays(-1); }
        return streak;
    }
}
```

注: `GetBreakdownForDayAsync` 用 Left Join（EF 里 `Join` 对 TaskId 可空的行为需验证）。若结果不稳定，改用手动逐行查 _db.Tasks 更稳。TaskId 为 null 的行显示"已删除任务"。

- [ ] **Step 3: 写测试验证聚合**

```csharp
using TomatoTime.Data;
using TomatoTime.Data.Entities;
using TomatoTime.Services;
using Xunit;

namespace TomatoTime.Tests;

public class StatsServiceTests
{
    private static IStatsService Create()
    {
        var db = TestDb.Create();
        return new StatsService(db);
    }

    [Fact]
    public async Task GetBreakdownForDayAsync_GroupsByTask_TitleFallback()
    {
        var db = TestDb.Create();
        var a = new TaskEntity { Title = "写文档", CreatedAt = DateTime.UtcNow, Order = 1 };
        db.Tasks.Add(a); db.SaveChanges();
        var now = DateTime.UtcNow;
        db.WorkSessions.Add(new() { TaskId = a.Id, StartedAt = now.AddMinutes(-25), EndedAt = now, DurationSeconds = 25 * 60 });
        db.WorkSessions.Add(new() { TaskId = null,   StartedAt = now.AddMinutes(-50), EndedAt = now.AddMinutes(-25), DurationSeconds = 25 * 60 });
        db.SaveChanges();
        var svc = new StatsService(db);
        var breakdown = await svc.GetBreakdownForDayAsync(DateTime.Today);
        Assert.Equal(2, breakdown.Count);
        Assert.Contains(breakdown, b => b.Title == "写文档" && b.Pomodoros == 1);
        Assert.Contains(breakdown, b => b.TaskId == null && b.Title == "已删除任务" && b.Pomodoros == 1);
    }

    [Fact]
    public async Task GetStreakDaysAsync_CountsConsecutive()
    {
        var db = TestDb.Create();
        var now = DateTime.UtcNow;
        db.WorkSessions.Add(new() { TaskId = null, StartedAt = now.AddDays(-1).AddMinutes(-25), EndedAt = now.AddDays(-1), DurationSeconds = 25 * 60 });
        db.WorkSessions.Add(new() { TaskId = null, StartedAt = now.AddMinutes(-25), EndedAt = now, DurationSeconds = 25 * 60 });
        db.SaveChanges();
        var svc = new StatsService(db);
        var streak = await svc.GetStreakDaysAsync(DateTime.Today.AddDays(-7), DateTime.Today);
        Assert.Equal(2, streak);
    }
}
```

- [ ] **Step 4: StatsViewModel — 三视图 + LiveCharts2**

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using TomatoTime.Services;

namespace TomatoTime.ViewModels;

public partial class StatsViewModel : ObservableObject
{
    [ObservableProperty] private int totalPomodoros;
    [ObservableProperty] private string totalDuration = "";
    [ObservableProperty] private string currentTask = "";
    [ObservableProperty] private int streakDays;
    [ObservableProperty] private string selectedRange = "日";

    private readonly IStatsService _svc;
    private ISeries[] _series = Array.Empty<ISeries>();
    public ISeries[] Series
    {
        get => _series;
        private set => SetProperty(ref _series, value);
    }

    public ObservableCollection<TaskBreakdown> BreakdownRows { get; } = new();

    public StatsViewModel(IStatsService svc) => _svc = svc;

    public async Task OnDaySelectedAsync(DateTime d)
    {
        TotalPomodoros = await _svc.GetPomodorosForAsync(d);
        var sec = await _svc.GetTotalSecondsForAsync(d);
        TotalDuration = $"{sec / 3600}h {(sec % 3600) / 60}m";
        // 24h 时间线柱：按每个 WorkSession 的 EndedAt 落进 0-23 小时桶
        var breakdown = await _svc.GetBreakdownForDayAsync(d);
        BreakdownRows.Clear();
        foreach (var r in breakdown) BreakdownRows.Add(r);
        // 画 24 桶柱形（具体取 buckets 数据见 implementer）
    }

    [RelayCommand]
    private async Task SelectDay() => await OnDaySelectedAsync(DateTime.Today);

    [RelayCommand]
    private async Task SelectWeek(DateTime weekStart) { /* 调 GetWeeklyAsync 组 7 天柱 */ }

    [RelayCommand]
    private async Task SelectMonth(int month) { /* 调 GetMonthlyAsync 组当月柱 */ }
}
```

注: LiveCharts2 的 24h 时间线柱：把当天每个 WorkSession 的 `EndedAt.ToLocalTime().Hour` 落进 0-23 桶，以每桶番茄数为 Y。周视图是 7 天、月视图是当月天数逐天番茄数为 Y。

- [ ] **Step 5: 统计 Tab XAML**

在 MainWindow.xaml 的统计 TabItem 里放:

```xml
<Grid>
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="*"/>
    <RowDefinition Height="*"/>
  </Grid.RowDefinitions>

  <StackPanel Orientation="Horizontal" Grid.Row="0" Margin="8">
    <RadioButton Content="日" GroupName="Range" IsChecked="True" Margin="0,0,8,0"
                 Checked="Day_Checked"/>
    <RadioButton Content="周" GroupName="Range" Checked="Week_Checked" Margin="0,0,8,0"/>
    <RadioButton Content="月" GroupName="Range" Checked="Month_Checked"/>
  </StackPanel>

  <StackPanel Orientation="Horizontal" Grid.Row="1" Margin="8">
    <TextBlock Text="{Binding Stats.TotalPomodoros}" FontSize="24"/>
    <TextBlock Text="番茄" Margin="8,0,16,0" VerticalAlignment="Bottom"/>
    <TextBlock Text="{Binding Stats.TotalDuration}" FontSize="24"/>
    <TextBlock Text="时长" Margin="8,0,16,0" VerticalAlignment="Bottom"/>
    <TextBlock Text="{Binding Stats.StreakDays}" FontSize="24"/>
    <TextBlock Text="连续" Margin="8,0,16,0" VerticalAlignment="Bottom"/>
  </StackPanel>

  <lvc:CartesianChart Series="{Binding Stats.Series}" Grid.Row="2" Margin="8"/>

  <DataGrid ItemsSource="{Binding Stats.BreakdownRows}" Grid.Row="3"
            AutoGenerateColumns="True" IsReadOnly="True" Margin="8"/>
</Grid>
```

加 `xmlns:lvc="https://livecharts.net"`（或 NuGet 包指定的 namespace）。

- [ ] **Step 6: 测试 + 构建验证 + 提交**

```bash
dotnet test
dotnet build
git add -A && git commit -m "feat: 统计服务聚合与日/周/月视图"
```

---

## Task 10: 设置窗

**Files:** `Views/SettingsWindow.xaml` / `.cs`、`ViewModels/SettingsViewModel.cs`

- [ ] **Step 1: SettingsViewModel**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TomatoTime.Data.Entities;
using TomatoTime.Services;

namespace TomatoTime.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;

    [ObservableProperty] private int workMinutes;
    [ObservableProperty] private int shortBreakMinutes;
    [ObservableProperty] private int longBreakMinutes;
    [ObservableProperty] private int longBreakInterval;
    [ObservableProperty] private double overlayOpacity;
    [ObservableProperty] private bool startWithWindows;
    [ObservableProperty] private bool restoreOnStartup;
    [ObservableProperty] private int bellVolume;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        // 从当前缓存载入初值
        workMinutes = settingsService.WorkMinutes;
        shortBreakMinutes = settingsService.ShortBreakMinutes;
        longBreakMinutes = settingsService.LongBreakMinutes;
        longBreakInterval = settingsService.LongBreakInterval;
        overlayOpacity = settingsService.OverlayOpacity;
        restoreOnStartup = settingsService.RestoreOnStartup;
        bellVolume = settingsService.BellVolume;
    }

    [RelayCommand]
    private async Task Save()
    {
        var settings = new SettingsEntity
        {
            Id = 1,
            WorkMinutes = WorkMinutes,
            ShortBreakMinutes = ShortBreakMinutes,
            LongBreakMinutes = LongBreakMinutes,
            LongBreakInterval = LongBreakInterval,
            OverlayOpacity = OverlayOpacity,
            RestoreOnStartup = RestoreOnStartup,
            StartWithWindows = StartWithWindows,
            BellVolume = BellVolume
        };
        _settingsService.Update(settings);
        ToggleStartupRegistry(StartWithWindows);
        _settingsService.Reload();
    }

    private const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static void ToggleStartupRegistry(bool enable)
    {
        var key = Registry.CurrentUser.OpenSubKey(RUN_KEY, writable: true)
                  ?? Registry.CurrentUser.CreateSubKey(RUN_KEY);
        if (enable)
            key.SetValue("TomatoTime", Environment.ProcessPath ?? "");
        else
            key.DeleteValue("TomatoTime", false);
    }
}
```

- [ ] **Step 2: SettingsWindow.xaml 表单布局**

时长用 Slider 或 NumericUpDown，遮罩不透明度 Slider (0.3-0.9)，铃声音量 Slider (0-100)，开机自启/启动恢复 CheckBox，底部一个保存按钮。

```xml
<Window x:Class="TomatoTime.Views.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="设置" Width="360" Height="500"
        WindowStartupLocation="CenterOwner">
  <StackPanel Margin="20">
    <Label Content="工作时长（分钟）"/>
    <Slider Minimum="5" Maximum="90" Value="{Binding WorkMinutes}" TickFrequency="5" IsSnapToTickEnabled="True"/>
    <Label Content="短休时长（分钟）"/>
    <Slider Minimum="1" Maximum="30" Value="{Binding ShortBreakMinutes}" TickFrequency="1" IsSnapToTickEnabled="True"/>
    <Label Content="长休时长（分钟）"/>
    <Slider Minimum="5" Maximum="60" Value="{Binding LongBreakMinutes}" TickFrequency="5" IsSnapToTickEnabled="True"/>
    <Label Content="长休间隔（每 N 个番茄）"/>
    <Slider Minimum="2" Maximum="8" Value="{Binding LongBreakInterval}" TickFrequency="1" IsSnapToTickEnabled="True"/>
    <Label Content="遮罩不透明度"/>
    <Slider Minimum="0.3" Maximum="0.9" Value="{Binding OverlayOpacity}" TickFrequency="0.1" IsSnapToTickEnabled="True"/>
    <Label Content="铃声音量"/>
    <Slider Minimum="0" Maximum="100" Value="{Binding BellVolume}" TickFrequency="10" IsSnapToTickEnabled="True"/>
    <CheckBox Content="开机自启" IsChecked="{Binding StartWithWindows}" Margin="0,10,0,0"/>
    <CheckBox Content="启动时恢复上次计时" IsChecked="{Binding RestoreOnStartup}" Margin="0,8,0,0"/>
    <Button Content="保存" Command="{Binding SaveCommand}" Padding="20,8" HorizontalAlignment="Right" Margin="0,16,0,0"/>
  </StackPanel>
</Window>
```

- [ ] **Step 3: 构建验证 + 提交**

```bash
dotnet build
git add -A && git commit -m "feat: 设置窗与开机自启"
```

---

## Task 11: 托盘 + 窗口协调

**Files:** `Services/IWindowService.cs` / `WindowService.cs`、`App.xaml`（托盘资源）、`App.xaml.cs`（启动迁移+恢复+悬浮）

- [ ] **Step 1: 用 H.NotifyIcon 实现托盘**

在 App.xaml 资源里:

```xml
<tb:TaskbarIcon x:Key="TrayIcon" IconSource="/Assets/app.ico"
                ToolTipText="TomatoTime"
                TrayLeftMouseDoubleClick="OnTrayToggle">
  <tb:TaskbarIcon.ContextMenu>
    <ContextMenu>
      <MenuItem Header="开始" Click="MnuStart"/>
      <MenuItem Header="暂停" Click="MnuPause"/>
      <MenuItem Header="跳过" Click="MnuSkip"/>
      <MenuItem Header="设置" Click="MnuSettings"/>
      <Separator/>
      <MenuItem Header="退出" Click="MnuExit"/>
    </ContextMenu>
  </tb:TaskbarIcon.ContextMenu>
</tb:TaskbarIcon>
```

加 `xmlns:tb="http://www.hardcodet.net/taskbar"`（或 H.NotifyIcon 对应 namespace）。

- [ ] **Step 2: IWindowService 实现**

```csharp
public interface IWindowService
{
    void ShowMain();
    void HideMain();
    void ToggleMain();
    void ShowFloating();
    void ShowSettings();
    void OnExit();
}

public class WindowService : IWindowService
{
    private readonly IServiceProvider _sp;
    private readonly ITimerService _timer;
    private readonly IStatePersistenceService _persist;
    private MainWindow? _main;
    private FloatingWindow? _floating;
    private SettingsWindow? _settings;

    public WindowService(IServiceProvider sp, ITimerService timer, IStatePersistenceService persist)
    { _sp = sp; _timer = timer; _persist = persist; }

    public void ShowMain()
    {
        _main ??= new() { DataContext = new MainViewModel(
            _sp.GetRequiredService<ITimerService>(),
            _sp.GetRequiredService<ITaskService>(),
            _sp.GetRequiredService<IStatsService>(),
            Application.Current.Dispatcher) };
        _main.Show();
        _main.Activate();
    }

    public void HideMain() => _main?.Hide();

    public void ToggleMain() { if (_main?.IsVisible == true) HideMain(); else ShowMain(); }

    public void ShowFloating()
    {
        _floating ??= new() { DataContext = new FloatingViewModel(
            _sp.GetRequiredService<ITimerService>(),
            Application.Current.Dispatcher) };
        _floating.Show();
    }

    public void ShowSettings()
    {
        _settings ??= new SettingsWindow
        {
            DataContext = new SettingsViewModel(_sp.GetRequiredService<SettingsService>()),
            Owner = _main
        };
        _settings.Show();
        _settings.Activate();
    }

    public void OnExit()
    {
        _persist.Save(_timer.State);
        if (_floating != null) _persist.SaveFloatingPosition(_floating.Left, _floating.Top);
        _floating?.Close();
        _settings?.Close();
        Application.Current.Shutdown();
    }
}
```

注: DI 注册用工厂提供 Dispatcher。上面直接 `Application.Current.Dispatcher` 是简化写法。把 `_persist.Save` 与 `SaveFloatingPosition` 合并写一次文件更好（见 Task 12 完善）。

- [ ] **Step 3: 主窗最小化隐藏而非退出**

```csharp
// MainWindow.xaml.cs
protected override void OnClosing(CancelEventArgs e)
{
    e.Cancel = true;
    Hide();
}

protected override void OnStateChanged(EventArgs e)
{
    if (WindowState == WindowState.Minimized) Hide();
}
```

注释: 这让"X" 按钮变成隐藏，只有托盘"退出"才真退出（调 IWindowService.OnExit）。

- [ ] **Step 4: App.xaml.cs 完整启动流程**

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    Services = ServiceConfiguration.Build();
    using var scope = Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TomatoTimeDbContext>();
    db.Database.Migrate();

    var persist = Services.GetRequiredService<IStatePersistenceService>();
    var timer = Services.GetRequiredService<ITimerService>();
    if (persist.Load() is { } loaded)
        timer.RestoreFrom(loaded);

    var win = Services.GetRequiredService<IWindowService>();
    win.ShowMain();
    win.ShowFloating();

    // 恢复悬浮窗位置
    var pos = persist.LoadFloatingPosition();
    if (pos is { } p)
    {
        var fw = Application.Current.Windows.OfType<FloatingWindow>().FirstOrDefault();
        if (fw != null) { fw.Left = p.x; fw.Top = p.y; }
    }
}
```

- [ ] **Step 5: 托盘事件处理**

```csharp
// App.xaml.cs
private void OnTrayToggle(object sender, RoutedEventArgs e)
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
```

- [ ] **Step 6: 构建验证 + 提交**

```bash
dotnet build
git add -A && git commit -m "feat: 托盘菜单与窗口协调"
```

---

## Task 12: 打磨、跨天检测、错误兜底、打包预留

**Files:** 各处零散修改

- [ ] **Step 1: 跨天自动刷新**

在 `MainViewModel` 订阅 `Tick` 的回调里顺手检测: 当前 `DateTime.Today != _lastDay` → 调 `Tasks.RefreshAsync()`。反正每秒执行，几乎零成本。

```csharp
private DateTime _lastDay = DateTime.Today;

private void RefreshDisplay()
{
    var r = _timer.State.RemainingSeconds;
    RemainingText = $"{r / 60:00}:{r % 60:00}";
    if (DateTime.Today != _lastDay)
    {
        _lastDay = DateTime.Today;
        Tasks.RefreshAsync();
    }
}
```

- [ ] **Step 2: 数据库迁移失败兜底**

`OnStartup` 的 `Migrate()` try-catch:

```csharp
try { db.Database.Migrate(); }
catch (Exception ex)
{
    // 备份旧库、清空重迁移最小方案
    MessageBox.Show($"数据库初始化失败:\n{ex.Message}\n已备份并重建。",
        "TomatoTime", MessageBoxButton.OK, MessageBoxImage.Warning);
    // 简单兜底: 重命名 tomato.db 为 .bak 再重迁
    var bakPath = AppPaths.DbPath + $".{DateTime.Now:HHmmss}.bak";
    if (File.Exists(AppPaths.DbPath)) File.Move(AppPaths.DbPath, bakPath);
    db.Database.Migrate();
}
```

- [ ] **Step 3: Toast 降级兜底**

`NotificationService.Notify`:
- 优先 `CommunityToolkit.WinUI.Notifications`（auto 判 OS≥10），失败回退 H.NotifyIcon `BalloonTip`。
- 首次启动用户没开通知权限: 不报错，仅靠响铃+遮罩兜底。

```csharp
public void Notify(string title, string body)
{
    try
    {
        // Toast: CommunityToolkit.WinUI.Notifications.ToastContentBuilder...
    }
    catch
    {
        // BalloonTip 降级 via H.NotifyIcon
        // _trayIcon.ShowNotification(title, body);
    }
}
```

- [ ] **Step 4: 响铃资源缺失**

若 `bell.wav` Embedded Resource 缺失:

```csharp
public void StartBell()
{
    try
    {
        _mp ??= new MediaPlayer();
        _mp.Open(new Uri("pack://application:,,,/Assets/bell.wav"));
        _mp.Volume = _settings.BellVolume / 100.0;
        _mp.MediaEnded += (s, e) => _mp.Position = TimeSpan.Zero;
        _mp.Play();
    }
    catch
    {
        // 降级 系统提示音
        System.Media.SystemSounds.Exclamation.Play();
    }
}
```

- [ ] **Step 5: 悬浮窗位置记忆完善**

`IStatePersistenceService` 增合并保存:

```csharp
public void Save(TimerState state, double floatX, double floatY)
{
    var p = new PersistedState(state.Phase, state.Status, state.RemainingSeconds,
                                state.CompletedPomodoros, state.ActiveTaskId, floatX, floatY);
    File.WriteAllText(_path, JsonSerializer.Serialize(p));
}
```

WindowService.OnExit 调合并版。

- [ ] **Step 6: 图标 + 提示音 + 空状态文案**

- `Assets/app.ico`: 红番茄图标。
- `Assets/bell.wav`: 嵌入资源（Build Action: Embedded Resource）。
- 任务空状态: "今日没有任务，加一个开始专注"。
- 统计空状态: "今天还没有完成番茄"。

- [ ] **Step 7: 可选打包 MSIX**

留 `<GeneratePackageOnBuild>` 配置但默认 false，在 `TomatoTime.csproj` 注释中给出 MSIX 包 `<PropertyGroup>` 片段。发布渠道接入后再放开。

- [ ] **Step 8: 最终全量构建与测试**

```bash
dotnet test
dotnet build --configuration Release
```
预期: 测试全绿、Release 构建成功。

- [ ] **Step 9: 提交**

```bash
git add -A && git commit -m "feat: 跨天刷新/错误兜底/资源与打磨"
```

---

## Self-Review Checklist

**1. 规格覆盖:**
- 计时循环（工作/短休/长休自动循环 + Waiting 确认） → Task 3 状态机 ✓
- 任务绑定（今日清单 + 激活 WorkSession 写入） → Task 4 + Task 6 ViewModel + Task 3 State.ActiveTaskId ✓
- 统计日/周/月 只读视图 → Task 9 IStatsService + StatsViewModel LiveCharts2 ✓
- 强制提醒（通知 + 响铃 + 遮罩） → Task 8 OverlayService/NotificationService ✓
- 悬浮倒计时 + 可拖动 → Task 7 FloatingWindow ✓
- 设置可配置（时长/透明度/开机自启/铃声音量） → Task 10 ✓
- 托盘 + 窗口协调 → Task 11 ✓
- 跨天清零（主窗只看今天） → Task 12 Step 1 ✓
- state.json 恢复（RestoreOnStartup） → Task 5 + Task 11 ✓

**2. 类型 / 方法一致性:**
- `ITimerService` 的方法名（Start/Pause/Resume/Skip/Stop/StartNext/Postpone/RestoreFrom）在 Task 3 与 Task 11 调用一致。
- `DecideNextPhase(PhaseKind, int, int)` internal static 在 Task 3 Step 13 与 Task 8 Step 2 引用一致。
- `PhaseEventArgs` 在 Task 3 Step 1 定义，Task 8 Step 6 加了 `PhaseStartedAt` 字段 — 追加，向后兼容。修改时确保 Task 3 的 EndPhase() 也填 PhaseStartedAt。
- `IStatePersistenceService` 在 Task 5 与 Task 11 都用 —— Task 11 Step 5 引入了合并保存版本，早于 Task 12 Step 5 的完善。实现时按 Task 12 Step 5 的签名统一改。
- 实体名一律 `TaskEntity`/`WorkSessionEntity`/`SettingsEntity`，无歧义残留。

**3. 留给实现者确认的口子:**
- Task 3 Step 17: "跳过工作段是否计入 CompletedPomodoros" — 规格里唯一留下由实现者决策的口子。计划默认自增以维持循环节奏。
- Task 9 Step 2: `GetBreakdownForDayAsync` 的 EF `Join` 对 TaskId 可空行为如果实际跑出结果不稳，改用逐行查 Tasks 表手动 join。

**4. 隐式假设:**
- LiveCharts2 版本 `2.0.0-rc2` 是写计划时的当前预览版；实际执行时取最新稳定版即 rc.x。
- H.NotifyIcon.Wpf 的 XAML namespace 写法可能随版本变化，以安装后 IntelliSense 为准。

---

## 执行移交

计划已保存至 `docs/superpowers/plans/2026-08-14-tomato-time-windows-desktop-plan.md`。两种执行方式:

1. **子代理驱动（推荐）** — 每个 Task 派一个新子代理实现，Task 间做两段审查。
2. **会话内执行** — 用 executing-plans 在本会话逐 Task 执行，带检点暂停。

**鉴于本环境为 Linux、目标产物是 Windows WPF 桌面应用:** 因为 .NET 8 跨平台编译，单元测试（Task 3/4/5/9）可以在 Linux 用 `dotnet test` 跑起来验证逻辑正确性；但 WPF UI 必须在 Windows 运行才能看到界面效果，悬浮/遮罩/托盘这些窗口化部分只能在 Windows 主机上验证。

**推荐执行路径:** 把此计划拷到 Windows 机器上后，先 `dotnet test` 让 Task 3 的状态机测试全绿 —— 这块逻辑最关键、风险最大；状态机稳了，UI 就是相对机械的绑定工作。StatsService 的聚合查询也在数据正确性上值得测过再继续。
