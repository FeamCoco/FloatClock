<div align="center">

# WorldClockBar

**Windows 多时区时钟条 · 常驻任务栏上方**

在任务栏上方常显多个国家/城市的时间（时分秒），WinUI / Fluent 风格界面，
深浅色自适应、强调色锁定品牌青，支持自由拖拽与边缘吸附。完全本地换算，**不访问网络**。

[简体中文](README.md) · [English](README.en.md)

![Version](https://img.shields.io/badge/version-1.2.0-0F766E)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4?logo=windows&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-green)

<img src="docs/images/clock-bar.png" alt="WorldClockBar 时钟条常驻任务栏上方" width="360"/>

</div>

> 💡 不注入系统任务栏 / Explorer，尽量避免与第三方任务栏插件冲突。

---

## ✨ 功能特性

| 功能 | 说明 |
|------|------|
| 多时区常显 | 城市成组：上行城市名 + 时差标识，下行大号 `HH:mm` 与小号秒；等宽数字防抖动 |
| Fluent 外观 | WinUI 风格：Acrylic 时钟条、Mica 设置窗口（Win11），Win10 自动降级纯色 |
| 品牌青主题 | 「经线 Meridian」品牌色 `#0F766E`；4 套预设（跟随系统/品牌浅色/品牌深色/高对比）+ 自定义颜色 |
| 本机换算 | 使用系统时钟 + `TimeZoneInfo`，**不访问网络** |
| 始终置顶 | 独立置顶窗口，定时巩固 Z 序，避免被普通窗口盖住 |
| 自由拖拽 | 可在**工作区**（任务栏以外区域）任意拖动 |
| 边缘吸附 | 靠近屏幕四边（含任务栏上沿）自动吸附，不会拖进任务栏 |
| 即时生效设置 | Win11 设置式界面：NavigationView + 卡片分组，改动即改即存，支持搜索 |
| 城市管理 | 行内改名、下拉换时区、⋮ 菜单排序/删除 |
| 多显示器 | 可选显示器；拖拽时可跨屏 |
| 开机自启 | 可选，写入当前用户 Run 注册表项 |
| 系统托盘 | 品牌托盘图标（随任务栏深浅色切换白/墨两版）；找不到窗口时可右键「显示时钟条」 |
| 单实例 | 防止重复启动 |

**默认配置：** 英国（`GMT Standard Time`）· 跟随系统深浅色 · 右下角贴边

---

## 🖼️ 界面预览

设置窗口采用 Windows 11 设置式布局（Mica 材质 + NavigationView 左导航 + 圆角卡片），所有改动即时生效：

| 时钟 | 个性化 |
|------|--------|
| ![设置窗口 · 时钟页](docs/images/settings.png) | ![设置窗口 · 个性化页](docs/images/settings-personalization.png) |

UI 按 Windows 11 / WinUI (Fluent Design) 规范设计（v2 品牌版）：

- **时钟条**：Acrylic 毛玻璃条，城市成组（上行城市名 + 时差标识 / 下行大号时间 + 小号秒），组间 26px 留白划分，本地城市带品牌青圆点，hover 单组高亮并显示完整日期与时区
- **设置窗口**：Mica 材质 + 自绘标题栏 + 导航顶部品牌标志 + NavigationView 左导航（时钟 / 个性化 / 行为 / 关于）+ Win11 设置式圆角卡片，选中态为品牌青指示条
- **全部控件**（开关、下拉、滑杆、右键菜单、Tooltip、滚动条）重绘为 Fluent 观感

品牌规格与图标资产见 [docs/brand/README.md](docs/brand/README.md)，早期三方案对比见 [docs/ui-redesign/design-proposals.md](docs/ui-redesign/design-proposals.md)。

---

## 📦 环境要求

| 用途 | 要求 |
|------|------|
| 运行 | Windows 10 / 11，[.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| 开发 / 编译 | [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) |

---

## 🚀 快速开始

### 编译出 exe（完整教程）

**第 1 步 · 安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)**（已装可跳过），安装后开个终端验证：

```powershell
dotnet --version    # 应输出 8.x
```

**第 2 步 · 获取源码**

```powershell
git clone https://github.com/FeamCoco/FloatClock.git
cd FloatClock
```

**第 3 步 · 编译（快速验证）**

```powershell
dotnet build -c Release
```

产物在 `src\WorldClockBar\bin\Release\net8.0-windows\WorldClockBar.exe`，双击即可运行。

**第 4 步 · 发布正式 exe**（按需三选一）

| 模式 | 命令 | 产物体积 | 适用场景 |
|------|------|----------|----------|
| ① 依赖框架 | 见下 | ≈ 0.4 MB | 目标机器已装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| ② 自包含 | 见下 | ≈ 160 MB 文件夹 | 目标机器**无需安装任何运行时** |
| ③ 自包含单文件 | 见下 | ≈ 154 MB 单个 exe | 只拷一个 exe 就能用的分发方式 |

```powershell
# ① 依赖框架 → publish\WorldClockBar.exe
dotnet publish src\WorldClockBar -c Release -r win-x64 --self-contained false -o publish

# ② 自包含 → publish-sc\WorldClockBar.exe（附带全部运行时文件）
dotnet publish src\WorldClockBar -c Release -r win-x64 --self-contained true -o publish-sc

# ③ 自包含单文件 → publish-sf\WorldClockBar.exe（单个 exe，拷走即用）
dotnet publish src\WorldClockBar -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish-sf
```

> 💡 三种方式的产物都是免安装的 `WorldClockBar.exe`，直接运行即可。
>
> 🛠️ 若发布时报 `NU1100: 无法解析 Microsoft.WindowsDesktop.App.Runtime.win-x64`，是 NuGet 源不可用所致：执行 `dotnet nuget list source` 检查，默认需能访问 nuget.org（内网可配置镜像源）。

### 开发调试运行

```powershell
dotnet run --project src\WorldClockBar -c Release
```

### 重置为默认配置

```powershell
.\WorldClockBar.exe --reset
```

会恢复默认：英国时区 + 跟随系统深浅色主题。

---

## 🖱️ 使用说明

| 操作 | 说明 |
|------|------|
| **左键拖动** | 在工作区内自由移动；靠近边缘自动吸附 |
| **双击** | 打开设置 |
| **右键** | 设置 / 选择显示器 / 开机自启 / 始终置顶 / 贴回右下角 / 退出 |
| **托盘图标** | 双击打开设置；右键可「显示时钟条」、退出 |
| **设置 → 时钟** | 城市行内改名、下拉换时区、⋮ 菜单排序删除；显示秒、时间格式、贴边对齐、显示器 |
| **设置 → 个性化** | 主题预设色卡、颜色、字体、字号、条高、圆角、透明度 |
| **设置 → 行为 / 关于** | 开机自启；配置文件位置、重置默认 |

> 所有设置**即时生效**并自动保存（无「应用/确定」按钮）。

### 设计原则：为何不易与其他工具冲突

- **不**修改 Explorer、**不**做 Deskband / 任务栏 Hook
- 独立 `Topmost` 工具窗口，根据显示器 **WorkingArea** 定位（任务栏区域被系统排除）
- 可与常见任务栏美化工具（如 StartAllBack、ExplorerPatcher 等）并存使用

> 说明：全屏独占游戏等场景下，系统可能仍会盖住置顶窗口，这是 Windows 限制。

---

## ⚙️ 配置文件

路径：

```text
%AppData%\WorldClockBar\settings.json
```

首次启动自动创建。主要字段：

```json
{
  "clocks": [
    { "label": "英国", "timeZoneId": "GMT Standard Time" }
  ],
  "appearance": {
    "themeName": "System",
    "background": "#EBFFFFFF",
    "foreground": "#FF14201E",
    "separatorColor": "#24000000",
    "fontFamily": "Segoe UI Variable Display, Segoe UI",
    "fontSize": 19,
    "opacity": 1.0,
    "barHeight": 60,
    "cornerRadius": 12,
    "horizontalAlignment": "Right",
    "offsetX": 4,
    "offsetY": 2,
    "freePosition": false,
    "posX": 0,
    "posY": 0,
    "edgeSnapDistance": 14
  },
  "monitor": { "usePrimary": true },
  "behavior": {
    "autoStart": false,
    "showSeconds": true,
    "alwaysOnTop": true,
    "timeFormat": "HH:mm:ss"
  }
}
```

| 字段 | 含义 |
|------|------|
| `clocks` | 城市显示名 + Windows 时区 ID |
| `appearance.themeName` | `System`（跟随系统）/ `Light`（品牌浅色）/ `Dark`（品牌深色）/ `HighContrast` / `Custom`（手动改过颜色） |
| `freePosition` / `posX` / `posY` | 拖拽后的自由位置（相对工作区） |
| `horizontalAlignment` + `offsetX/Y` | 非自由位置时的贴边对齐 |
| `edgeSnapDistance` | 边缘吸附距离（DIP） |

旧版本配置文件无需修改即可升级：缺失字段自动取默认值；`themeName` 缺省视为 `System`（跟随系统深浅色）；
旧的装饰性预设 `MistBlue` / `WarmSand` / `Glass` 已随品牌 v2 移除，读到就迁到品牌浅色。`separatorColor` 字段保留但不再渲染（城市之间已改为留白划分）。

时区 ID 可在设置界面的下拉列表中选择，无需手写。

---

## 🔄 开机自启

在设置或右键菜单中开启。写入：

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run\WorldClockBar
```

关闭开关，或删除该注册表值即可取消。

---

## 🧱 项目结构

```text
FloatClock/
├── WorldClockBar.sln
├── README.md                    # 简体中文文档
├── README.en.md                 # English documentation
├── docs/ui-redesign/            # UI 设计规格与高保真预览
│   ├── design-proposals.md
│   ├── mockups.html
│   └── settings-mockups.html
├── docs/images/                 # README 截图
└── src/WorldClockBar/
    ├── App.xaml(.cs)              # 入口、单实例、主题初始化
    ├── MainWindow.xaml(.cs)       # 时钟条主窗口（Acrylic 材质）
    ├── SettingsWindow.xaml(.cs)   # 设置界面（NavigationView + 卡片，即时生效）
    ├── Styles/Fluent.xaml         # Fluent 控件样式库
    ├── Models/                    # 配置模型
    └── Services/
        ├── ThemeService.cs           # 深浅色/强调色检测、Mica/Acrylic 材质
        ├── SettingsService.cs        # JSON 读写
        ├── TimeDisplayService.cs     # 时区换算
        ├── WindowPlacementService.cs # 定位 / 拖拽 / 边缘吸附
        ├── AutostartService.cs       # 开机自启
        ├── TrayIconService.cs        # 系统托盘
        └── ColorHelper.cs            # 颜色解析（容错回退）
```

技术栈：**.NET 8 + WPF**（辅助使用 WinForms 的 `Screen` / 托盘 / 颜色对话框）。

**材质说明：** Win11 22H2+ 通过 `DwmSetWindowAttribute` 启用 Mica（设置窗口）与 Acrylic（时钟条，`SetWindowCompositionAttribute`，拖拽时自动切换为纯色防拖影）；Win10 自动降级为纯色半透明，功能不受影响。深浅色与强调色实时跟随系统。

---

## ❓ 常见问题

**Q：启动后看不到时钟条？**
A：查看系统托盘是否有 WorldClockBar 图标 → 右键「显示时钟条」。或使用 `--reset` 恢复默认位置与样式。

**Q：时间准吗？要联网吗？**
A：完全按本机系统时间换算，不联网。请保证 Windows 时间本身准确即可。

**Q：能塞进系统任务栏时钟区域吗？**
A：本项目故意不注入任务栏，以降低与美化/插件的冲突。显示方式是贴在任务栏**上方**的独立条。

**Q：如何完全卸载？**
A：退出程序 → 删除可执行文件目录 → 可选删除 `%AppData%\WorldClockBar` 与上述 Run 注册表项。

---

## 📄 许可证

[MIT](LICENSE) — 可自由使用、修改与分发。

---

## 🙏 致谢

- Windows [`TimeZoneInfo`](https://learn.microsoft.com/dotnet/api/system.timezoneinfo) 提供夏令时与时区换算
- 定位基于显示器 **WorkingArea**，避免侵入系统任务栏
