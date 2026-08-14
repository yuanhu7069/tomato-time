# TomatoTime - Windows 桌面番茄钟 应用设计规格

## 概述

一个 Windows 桌面番茄钟应用,主要面向自用,同时为后续给朋友或公开发布留扩展余地。核心功能:计时(工作/短休/长休自动循环)、任务(今日清单 + 绑定到番茄)、统计(日/周/月专注情况)、强制提醒(系统通知 + 响铃 + 遮罩)、悬浮(关闭主窗后悬浮一个可拖动倒计时)。

本环境为 Linux 沙箱,无法编译运行 Windows 程序,本文档仅产出设计规格与开发 tasks 指导;实际编码在 Windows 机器上进行。

## 目标与非目标

### 目标

- 单进程常驻托盘的 Windows 桌面应用。
- 计时按工作/短休/长休标准番茄循环运转,段间用遮罩确认后再进入下一段。
- 任务绑定到工作段,记录每个任务的专注投入。
- 记录已完成工作段流水,提供日/周/月统计视图。
- 段结束时强制提醒(系统通知 + 响铃 + 遮罩)。
- 主窗可最小化到托盘,同时保留一个可拖动的悬浮倒计时窗口。

### 非目标

- 不做云同步、不做账号体系、不做跨设备协作。数据仅在本地 SQLite。
- 不做分钟级原始时间热力图、不做完成率/打断维度等高级统计。
- 不做自动更新服务、不做在线安装包分发渠道(MVP 不直接做,但代码组织留余地)。
- 不做全局热键控制(避免快捷键冲突)。
- 不做数据导入,仅做必要的本地使用。
- 不做任务历史明细列表(主窗只看今天;跨天自动清零)。

## 技术栈

- 语言:C# (.NET 8)
- UI 框架:WPF
- MVVM:CommunityToolkit.Mvvm + Microsoft.Extensions.DependencyInjection
- 持久化:SQLite via EF Core (`Microsoft.Data.Sqlite` provider)
- 图表:LiveCharts2
- 项目命名(建议):`TomatoTime`

## 窗口模型

单进程,常驻托盘。四个窗口共享同一份 `TimerState` 内存快照。

### MainWindow(三段式宿主)

默认可见。顶部计时视图,中部任务列表,底部/Tab 切换至统计页。可最小化到托盘,但**不真正退出**(退出仅在托盘菜单中提供)。Tab 区三个页签:**今日待办**、**统计**、**设置**的入口。

### FloatingWindow(悬浮)

`Topmost` 可拖动小窗口,显示倒计时 + 当前任务名,带一个展开按钮(展开 MainWindow)。主窗隐藏时仍保留悬浮;关闭主窗不退出程序而是最小化到托盘并保留悬浮。

### OverlayWindow(遮罩)

`Topmost` 满屏半透明窗口(Opacity = Settings.OverlayOpacity,默认 0.7)。覆盖主显示器工作区,留出任务栏。段结束时弹出,居中显示"段已结束 / 下一段名 / 提示",两个按钮:**开始下一段**、**稍后1分**。遮罩与悬浮窗不互斥,可同时存在。

### SettingsWindow(设置)

独立窗口,从主窗菜单或托盘菜单进入。修改时长、长休间隔、遮罩透明度、开机自启、启动恢复、铃声音量等偏好。

### 托盘菜单

左键:切换主窗显示/隐藏。右键菜单项:开始/暂停/跳过/设置/退出。

## 计时状态机

显式状态机驱动所有窗口行为,避免散落的时间逻辑。核心状态五个:

- **Idle**:未开始
- **Working**:工作段计时中,时间记到当前激活任务
- **Break**:休息段计时中(Short/Long 由 `PhaseKind` 枚举区分,不另开状态)
- **Paused**:暂停,保留剩余时间,不改变归属
- **Waiting**:段结束,遮罩已弹,等用户决定下一段

### 转移规则

1. **Idle → Working**:用户点"开始"。
2. **Working/Break 剩余=0 → Waiting**:
   - 触发系统通知 + 响铃 + 弹遮罩。
   - 若刚结束的是 Working,记录一条 `WorkSession` 流水,已完成番茄数 +1。
3. **Waiting + "开始下一段"**:按"已完成番茄数 `n` mod 长休间隔 `N`"判定下一段:
   判定取决于刚结束的是哪个段:
   - 刚结束的是 **Working**(进 Waiting 前已完成番茄数 `n` 已自增):
     - `n mod N == 0` → Break(Long)
     - 否则 → Break(Short)
   - 刚结束的是 **Break**(Short 或 Long,`n` 不变):
     - → Working
   转入对应段并开始计时。下一个 Working 开始时把"本段已工作时长"从 0 起计;新段进度与上一段无继承。
4. **Waiting + "稍后1分"**:遮罩关,进入 1 分钟延迟,期到再次弹遮罩 + 响铃(不重发 Toast)。Waiting 保持,不计时,不计入工作段。
5. **Working/Break → Paused**:用户点暂停,冻结剩余时间。
6. **Paused → Working/Break**:用户点继续,恢复原相位,剩余时间不变。
7. **任意 → 跳过**:用户点"跳过当前段",直接当作剩余=0 触发转移 2 的后半(计/不计流水、切下一段),**不经过 Waiting,不弹遮罩**。跳过是主动放弃当前段。
8. **任意 → 停止**:回 Idle。进行中段不计流水(已完成的段保留),已完成番茄数清零。state.json 在程序退出时按当前内存状态写入;若已停止则记 Idle,下次启动由 RestoreOnStartup 也只恢复到 Idle(已停止的循环不会复活)。

### 暂停中到点

不会发生——暂停时计时器停摆,剩余冻结,不自然归 0。

## 软件架构

分层(项目目录建议):

```
TomatoTime/
+- Views/              # *.xaml + 代码后置
+- ViewModels/         # MainViewModel, TasksViewModel, StatsViewModel,
|                      #   FloatingViewModel, RestViewModel(遮罩), SettingsViewModel
+- Services/           # ITimerService, ITaskService, IStatsService,
|                      #   INotificationService, IOverlayService,
|                      #   IFloatingService, IWindowService
+- Data/               # EF Core DbContext + Entities/ (Task, WorkSession, Settings)
+- Models/             # TimerState, PhaseKind 等运行时模型
+- Assets/             # 提示音 wav、图标等嵌入资源
```

### 服务职责

- **ITimerService**:计时核心。持有 `TimerState`,每秒 `Tick`,段结束时抛 `PhaseEnded`/`PhaseStarted` 事件。提供 `Start`/`Pause`/`Resume`/`Skip`/`Stop`/`StartNext`/`Postpone` 等命令。
- **ITaskService**:任务 CRUD + 激活/完成切换,保证同一时刻只有一个 `IsActive=true` 的任务。
- **IStatsService**:查询 `WorkSession` 流水聚合(日/周/月),供 StatsViewModel 渲染。
- **INotificationService**:系统 Toast 通知 + 响铃(`SoundPlayer` 循环,可单独设音量)。
- **IOverlayService**:管理遮罩窗的显示/隐藏、按钮响应转发到 `ITimerService`。
- **IFloatingService**:管理悬浮窗生命周期与位置记忆。
- **IWindowService**:协调主窗 ↔ 托盘 ↔ 悬浮 ↔ 遮罩之间的显示/隐藏。

### 事件与状态共享

`ITimerService` 推事件:`PhaseEnded`、`PhaseStarted`、`Tick`、`Waiting`、`Skipped`。
主窗/悬浮窗 VM 订阅 `Tick` 刷倒计时;`IOverlayService` 订阅 `PhaseEnded` 弹遮罩。
所有窗口共享同一 `TimerState` 快照(当前段、剩余秒、状态、已完成番茄数、当前激活任务 ID),保证显示一致。

## 数据模型

数据库文件:`%AppData%\TomatoTime\tomato.db`,SQLite,EF Core Code-First 迁移。

### Task

- `Id` (int, PK, 自增)
- `Title` (string, 必填)
- `CreatedAt` (DateTime, UTC)
- `IsActive` (bool) — 同一时刻至多一行 true;由 `ITaskService` 在事务中切换,不依赖唯一索引。
- `CompletedAt` (DateTime?) — 完成则填本地日期对应 UTC。
- `Order` (int) — 客户端手动排序;MVP 保留,便于发布后排任务。

### WorkSession(已完成工作段流水)

- `Id` (int, PK, 自增)
- `TaskId` (int, FK → Task.Id, 可空 — 任务删除时仍保留记录)
- `StartedAt` (DateTime, UTC)
- `EndedAt` (DateTime, UTC)
- `DurationSeconds` (int) — 冗余存储,避免夏令时边角,简化聚合。

仅 Working 段自然归 0 时插入一条。暂停、稍后、停止时正在进行的段一律不入库。

### Settings(单行表,Id 固定 = 1)

- `WorkMinutes` (int, 默认 25)
- `ShortBreakMinutes` (int, 默认 5)
- `LongBreakMinutes` (int, 默认 15)
- `LongBreakInterval` (int, 默认 4)
- `OverlayOpacity` (double, 默认 0.7)
- `RestoreOnStartup` (bool, 默认 true)
- `StartWithWindows` (bool, 默认 false) — 写注册表 `Run` 项。
- `BellVolume` (int 0-100, 默认 70)

### 运行时状态(state.json)

文件路径:`%AppData%\TomatoTime\state.json`。存放运行中易逝状态,不入库:

- 当前段(PhaseKind)
- 剩余秒
- 状态(Idle/Working/Break/Paused/Waiting)
- 已完成番茄数
- 当前激活 TaskId

持久化时机:

- 程序退出时写。
- 启动时按 `Settings.RestoreOnStartup` 决定是否恢复。
- 不写库,避免与 EF DbContext 业务语义混淆。

## 任务面板

主窗"今日待办"页一块面板内两部分共存:

### 今日待办

`CompletedAt IS NULL` 的任务,默认展开。每条右侧显示今天为其完成的番茄数(来自 WorkSession 当日聚合)。点击勾选完成 → 移到下方"今日已完成"。

### 今日已完成

`CompletedAt` 为今天的任务,默认折叠,标题带计数 `今日已完成 (N)`。展开后每条带删除线 + 今天为其完成的番茄数。

"今日"按本地日期判定,不看 UTC。

### 跨天行为

跨天后今日待办/今日已完成都按本地日期重新清零,主窗只看"今天"。任务实体保留,历史明细走统计页查询。

历史任务明细列表不在主窗呈现。

## 统计页

主窗 Tab 之一,只读视图。数据来源全是从 WorkSession 流水临时聚合,不另存聚合表。三个维度:**日 / 周 / 月**,不做全历史。

### 日视图(默认显示今天)

- 顶部一行 KPI:今日番茄数、今日总专注时长、当前激活任务名。
- 下方柱状图:X 轴为 24 小时时间线,每个完成的番茄在对应时段画一柱,高度固定一个单位。直观体现"一天里番茄都集中在哪些时段"。
- 底部"按任务分布"小表:当天涉及的任务 → 标题、为之完成的番茄数、为之专注时长,按番茄数降序。TaskId 为空时显示"已删除任务"。

### 周视图

- 选择"本周/最近 7 天",柱状图 X 轴 7 天,柱高为当天番茄数。
- 下方一行小数:本周总番茄、本周总时长、本周日均、最长连续天数(至少完成 1 个番茄的连续天数)。
- 底部"按任务分布"小表同日视图。

### 月视图

- 选择月份,柱状图 X 轴该月每一天,柱高为当天番茄数。
- 下方一行小数:本月总番茄、本月总时长、本月日均、最长连续天数。
- 底部"按任务分布"小表同日视图。

### 技术选型

图表用 **LiveCharts2**:轻量、现代、API 友好,柱状/简单图都好写。

## 设置与提醒

### 设置窗字段

- 工作时长(分钟)、短休时长、长休时长、长休间隔 N
- 遮罩不透明度(0.3-0.9 滑条)
- 开机自启(boolean)
- 启动时恢复上次计时(boolean)
- 铃声音量(0-100)

### 提醒实现细节

- 系统通知:WPF 走 `CommunityToolkit.WinUI` 的通知封装,或退回 `NotifyIcon.ShowBalloonTip`。
- 响铃:`SoundPlayer` 或 `MediaPlayer` 循环播放嵌入资源中的一段小提示音(.wav),点遮罩后停止。音量受 `Settings.BellVolume` 单独控制。

### 提醒流程(段结束时)

1. Working 剩余=0 → 写 WorkSession 流水 + 已完成番茄数 +1 → 触发 `PhaseEnded`。
2. `INotificationService` 弹 Toast + `SoundPlayer` 循环响铃。
3. `IOverlayService` 显示 OverlayWindow(Topmost,Opacity = Settings.OverlayOpacity),居中显示段已结束信息与下一段名。
4. 用户点"开始下一段":停响铃 → 关遮罩 → `ITimerService.StartNext()` 进入下一段计时。
5. 用户点"稍后1分":停响铃 → 关遮罩 → 进入 1 分钟延迟;期到再次弹遮罩 + 响铃(不重发 Toast,避免污染通知中心)。

## 开发任务(tasks)

以下任务作为 Windows 上开发的指导。Linux 环境不执行编码。

### T1 项目骨架

- T1.1 创建 `TomatoTime` WPF .NET 8 项目,启用 CommunityToolkit.Mvvm 与 Microsoft.Extensions.DependencyInjection。
- T1.2 引入 EF Core + `Microsoft.Data.Sqlite`、LiveCharts2、托盘图标库(如 `H.NotifyIcon.Wpf` 或 `Hardcodet.NotifyIcon.Wpf`)。
- T1.3 建立目录结构:Views/ViewModels/Services/Data/Models/Assets。
- T1.4 配置 DI 容器:注册各服务接口与 ViewModel,设置主窗启动。

### T2 数据层

- T2.1 定义三个实体(Task / WorkSession / Settings)与映射。
- T2.2 实现 `TomatoTimeDbContext`,配置关系(Task → WorkSession 一对多,WorkSession.TaskId 可空,删除任务时设 TaskId 为空)。
- T2.3 EF Core Code-First 迁移,数据库路径 `%AppData%\TomatoTime\tomato.db`。
- T2.4 `ISettingsService` 负责读取/更新单行 Settings 表,缓存到内存供各处读取。

### T3 计时核心

- T3.1 实现 `TimerState`(段、剩余秒、状态、已完成番茄数、激活任务 ID)与 `PhaseKind` 枚举(Work/ShortBreak/LongBreak)。
- T3.2 实现 `ITimerService`:每秒 Tick,段结束抛 PhaseEnded / PhaseStarted 事件。实现 Start/Pause/Resume/Skip/Stop/StartNext/Postpone 命令。
- T3.3 状态机转移逻辑全部落在本服务内,UI 与其它服务不直接判定下一段。
- T3.4 state.json 读写(退出时保存、启动时按 RestoreOnStartup 恢复)。

### T4 任务服务

- T4.1 `ITaskService`:CRUD + 激活切换(事务内把旧的 IsActive=false、新的 true) + 完成切换(填 CompletedAt)。
- T4.2 今日查询:CompletedAt IS NULL、CompletedAt 为今天两个列表,按 Order/Order+CreatedAt 排序。
- T4.3 WorkSession 写入:段完成时插入一条(TaskId、StartedAt/EndedAt UTC、DurationSeconds)。

### T5 主窗 UI

- T5.1 三段式布局:顶部计时视图(大倒计时 + 段名 + 开始/暂停/跳过按钮),中部任务面板,底部 Tab 切换。
- T5.2 TasksViewModel:今日待办(默认展开)+ 今日已完成(默认折叠,带计数)。每条显示今天番茄数。
- T5.3 勾选完成、激活切换、新增/删除任务的命令绑定。
- T5.4 跨天检测:本地日期变化时刷新今日两个列表。

### T6 悬浮窗

- T6.1 FloatingWindow:Topmost 可拖动小窗,显示倒计时 + 任务名 + 展开按钮。
- T6.2 FloatingViewModel 订阅 Tick 刷新显示。
- T6.3 位置记忆(退出时存 state.json,启动恢复)。

### T7 遮罩窗

- T7.1 OverlayWindow:Topmost 满屏半透明,留出任务栏,居中显示段结束信息 + 下一段名 + 两个按钮。
- T7.2 IOverlayService 订阅 PhaseEnded,弹遮罩 + 通知 + 响铃。
- T7.3 "开始下一段"→ 关遮罩 + StartNext;"稍后1分"→ 关遮罩 + Postpone。
- T7.4 稍后期到再次弹遮罩 + 响铃,不重发 Toast。

### T8 系统通知与响铃

- T8.1 INotificationService:Toast 通知(或 BalloonTip 降级) + 循环响铃。
- T8.2 响铃音量受 Settings.BellVolume 控制,点遮罩按钮后停止。

### T9 统计页

- T9.1 StatsViewModel:IStatsService 查询日/周/月聚合。
- T9.2 KPI 行:番茄数、专注时长、当前任务名(日视图)、总/日均/连续天数(周/月视图)。
- T9.3 LiveCharts2 柱状图:日 24h 时间线 / 周 7 天 / 月 30 天。
- T9.4 "按任务分布"小表,TaskId 为空显示"已删除任务"。

### T10 设置窗

- T10.1 SettingsViewModel:绑定 Settings 各字段,保存按钮整体更新。
- T10.2 开机自启写注册表 Run 项。
- T10.3 启动恢复切换决定 state.json 加载行为。

### T11 托盘与窗口协调

- T11.1 IWindowService:主窗最小化到托盘、托盘左键切换主窗显隐、右键菜单(开始/暂停/跳过/设置/退出)。
- T11.2 退出时全量写 state.json,关闭悬浮/遮罩/主窗/托盘。

### T12 打磨与发布预留

- T12.1 图标、提示音、空状态文案。
- T12.2 错误处理:数据库迁移失败、Toast 降级、响铃资源缺失的兜底。
- T12.3 可选:MSIX 打包配置(发布渠道后启用),安装包留口子不实现。
