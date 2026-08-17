# 🍅 TomatoTime — Windows 桌面番茄钟

一个单进程常驻托盘的 Windows 桌面番茄钟应用。核心功能：**计时循环、今日任务、专注统计、强制提醒（通知 + 响铃 + 遮罩）、可拖动悬浮窗**，采用深色科技感 UI。

## 功能特性

- **标准番茄循环**：工作 / 短休 / 长休自动交替（长休由已完成番茄数决定，配置项可调间隔）
- **今日任务**：当天新建任务清单，支持添加 / 行内编辑 / 删除 / 设为当前任务；跨天自动清零，昨天的待办不带到今天
- **专注统计**：日 / 周 / 月视图 + 柱状图（LiveCharts2）+ 按任务分布表，数据来自本地 SQLite
- **强制提醒**：段结束弹出全屏遮罩（实心深色背景，按钮清晰）+ 系统托盘通知（BalloonTip）+ 循环响铃，支持"开始下一段 / 稍后 1 分"
- **悬浮窗**：可拖动、透明的实时倒计时小窗，主窗隐藏后依旧保留，双击 / 右键可展开主窗或退出
- **系统托盘**：关闭主窗隐藏到托盘，右键菜单提供 开始 / 暂停 / 跳过 / 设置 / **退出**
- **设置**：工作 / 短休 / 长休时长、长休间隔、铃声音量、开机自启、启动恢复上次计时
- **纯本地**：数据仅存本地 SQLite，无云同步、无账号

## 技术栈

| 层 | 技术 |
| --- | --- |
| 语言 / 框架 | C# / .NET 8 (WPF, `net8.0-windows`) |
| MVVM | CommunityToolkit.Mvvm + Microsoft.Extensions.DependencyInjection |
| 持久化 | SQLite via EF Core（`Microsoft.EntityFrameworkCore.Sqlite`，Code-First 迁移） |
| 图表 | LiveChartsCore.SkiaSharpView.WPF |
| 托盘 | H.NotifyIcon.Wpf |
| 测试 | xUnit + 真实 SQLite in-memory（支持事务/关系验证） |

## 环境要求

- Windows 10/11（64 位）
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（构建）

## 构建与运行

```bash
# 还原并构建 Release
dotnet build -c Release

# 运行
dotnet run --project TomatoTime -c Release
# 或直接运行编译产物
TomatoTime\bin\Release\net8.0-windows\TomatoTime.exe
```

> 提示：如果构建报 `TomatoTime.exe ... 被另一进程占用`，先退出正在运行的番茄钟实例（托盘右键 → 退出）再重新构建。

## 使用方法

1. 启动后主窗顶部显示倒计时；点 **开始** 进入工作段
2. 在 **今日待办** 添加任务，勾选设为当前任务，工作段结束时该任务计入一个番茄
3. 段结束弹出遮罩：**开始下一段**（自动进入休息/工作）或 **稍后 1 分**
4. 关闭主窗（X）→ 最小化到托盘，悬浮窗保留倒计时；托盘左键切换主窗显隐
5. **统计** Tab 查看日 / 周 / 月的番茄数与专注时长

## 数据存储

本地数据保存在系统应用数据目录：

```
%APPDATA%\TomatoTime\
├── tomato.db      # SQLite 数据库(任务/工作流水/设置)
└── state.json     # 退出时的计时状态与悬浮窗位置(供下次启动恢复)
```

## 目录结构

```
TomatoTime/
├── Views/          # WPF 窗口(Main/Floating/Overlay/Settings) + XAML
├── ViewModels/     # MVVM 视图模型
├── Services/       # 计时状态机/任务/统计/通知/遮罩/悬浮/窗口/持久化 等
├── Data/           # EF Core DbContext + 实体(Task/WorkSession/Settings)
├── Models/         # TimerState、PhaseKind 等运行时模型
├── Migrations/     # EF Core Code-First 迁移
└── Assets/         # 提示音 bell.wav、图标 app.ico
TomatoTime.Tests/   # xUnit 单元测试(状态机/任务/统计/持久化)
```

## 测试

```bash
dotnet test
```

覆盖核心逻辑：计时状态机（工作/休息/暂停/跳过/稍后转移）、任务 CRUD 与激活唯一性、今日待办跨天过滤、统计聚合（日/周/月、按任务分布、连续天数、小时桶）、状态持久化、时长格式化。

## 发布预留

项目已在 csproj 中预留 MSIX 打包配置开关（默认关闭）。接入发布渠道时按需启用；`Assets` 内图标已含多尺寸（16-256px），可直接用于 exe / 窗口 / 托盘 / 打包。

## 许可

本仓库为个人自用项目，未指定开源协议。
