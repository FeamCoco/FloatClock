# WorldClockBar

Windows 多时区时钟条：在**任务栏上方**常显其他国家/城市时间（时分秒），WinUI / Fluent 风格界面，支持深浅色与系统强调色自适应、自由拖拽与边缘吸附。

> 不注入系统任务栏 / Explorer，尽量避免与第三方任务栏插件冲突。

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?logo=windows&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-green)

---

## 功能特性

| 功能 | 说明 |
|------|------|
| 多时区常显 | 横向显示多个城市，格式 `HH:mm:ss`，秒数弱化、等宽数字防抖动 |
| Fluent 外观 | WinUI 风格：Acrylic 时钟条、Mica 设置窗口（Win11），Win10 自动降级纯色 |
| 主题系统 | 跟随系统深浅色 + 系统强调色；7 套预设（浅色/深色/高对比/雾蓝/暖砂/玻璃）+ 自定义颜色 |
| 本机换算 | 使用系统时钟 + `TimeZoneInfo`，**不访问网络** |
| 始终置顶 | 独立置顶窗口，定时巩固 Z 序，避免被普通窗口盖住 |
| 自由拖拽 | 可在**工作区**（任务栏以外区域）任意拖动 |
| 边缘吸附 | 靠近屏幕四边（含任务栏上沿）自动吸附，不会拖进任务栏 |
| 即时生效设置 | Win11 设置式界面：NavigationView + 卡片分组，改动即改即存，支持搜索 |
| 城市管理 | 行内改名、下拉换时区、⋮ 菜单排序/删除 |
| 多显示器 | 可选显示器；拖拽时可跨屏 |
| 开机自启 | 可选，写入当前用户 Run 注册表项 |
| 系统托盘 | 找不到窗口时可右键托盘「显示时钟条」 |
| 单实例 | 防止重复启动 |

**默认配置：** 英国（`GMT Standard Time`）· 跟随系统深浅色 · 右下角贴边

---

## 界面设计

UI 按 Windows 11 / WinUI (Fluent Design) 规范重新设计（v1.1）：

- **时钟条**：Acrylic 毛玻璃细长条，城市名 60% 透明度分层，秒数弱化，hover 单城市高亮并显示完整日期与时区
- **设置窗口**：Mica 材质 + 自绘标题栏 + NavigationView 左导航（时钟 / 个性化 / 行为 / 关于）+ Win11 设置式圆角卡片
- **全部控件**（开关、下拉、滑杆、右键菜单、Tooltip、滚动条）重绘为 Fluent 观感

设计规格与三方案对比见 [docs/ui-redesign/design-proposals.md](docs/ui-redesign/design-proposals.md)，高保真预览见 [docs/ui-redesign/mockups.html](docs/ui-redesign/mockups.html)。

---

## 截图

<!-- 上传到 GitHub 后可替换为真实截图路径，例如：docs/screenshot.png -->

```
┌─────────────────────────────────────────────────────────────┐
│  桌面                                                       │
│                                                             │
│                                                             │
│                                    ┌──────────────────┐     │
│                                    │ 英国 20:15:33    │ ← 时钟条（可拖）
└────────────────────────────────────┴──────────────────┴─────┘
│  开始  ·  ·  ·  ·  ·  ·  ·  ·  ·  ·  ·  ·  ·  ·  系统托盘时间 │
└─────────────────────────────────────────────────────────────┘
```

---

## 环境要求

| 用途 | 要求 |
|------|------|
| 运行 | Windows 10 / 11，[.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| 开发 / 编译 | [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) |

---

## 快速开始

### 从源码运行

```powershell
git clone https://github.com/FeamCoco/FloatClock.git
cd FloatClock
dotnet build -c Release
dotnet run --project src\WorldClockBar -c Release
```

或直接运行编译产物：

```powershell
.\src\WorldClockBar\bin\Release\net8.0-windows\WorldClockBar.exe
```

### 发布可执行文件

**依赖本机已安装 .NET 8（体积更小）：**

```powershell
dotnet publish src\WorldClockBar -c Release -r win-x64 --self-contained false -o publish
# 运行：publish\WorldClockBar.exe
```

**自包含（目标机器可不装运行时）：**

```powershell
dotnet publish src\WorldClockBar -c Release -r win-x64 --self-contained true -o publish-sc
```

### 重置为默认配置

```powershell
.\WorldClockBar.exe --reset
```

会恢复默认：英国时区 + 跟随系统深浅色主题。

---

## 使用说明

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

### 设计原则（为何不易冲突）

- **不**修改 Explorer、**不**做 Deskband / 任务栏 Hook  
- 独立 `Topmost` 工具窗口，根据显示器 **WorkingArea** 定位（任务栏区域被系统排除）  
- 可与常见任务栏美化工具（如 StartAllBack、ExplorerPatcher 等）并存使用  

> 说明：全屏独占游戏等场景下，系统可能仍会盖住置顶窗口，这是 Windows 限制。

---

## 配置文件

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
    "background": "#E9FAFBFC",
    "foreground": "#FF1B1B1B",
    "separatorColor": "#24000000",
    "fontFamily": "Segoe UI Variable Display, Segoe UI",
    "fontSize": 15,
    "opacity": 1.0,
    "barHeight": 40,
    "cornerRadius": 8,
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
| `appearance.themeName` | `System`（跟随系统）/ `Light` / `Dark` / `HighContrast` / `MistBlue` / `WarmSand` / `Glass` / `Custom`（手动改过颜色） |
| `freePosition` / `posX` / `posY` | 拖拽后的自由位置（相对工作区） |
| `horizontalAlignment` + `offsetX/Y` | 非自由位置时的贴边对齐 |
| `edgeSnapDistance` | 边缘吸附距离（DIP） |

旧版本配置文件无需修改即可升级：缺失字段自动取默认值；`themeName` 缺省视为 `System`（跟随系统深浅色）。

时区 ID 可在设置界面的下拉列表中选择，无需手写。

---

## 开机自启

在设置或右键菜单中开启。写入：

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run\WorldClockBar
```

关闭开关，或删除该注册表值即可取消。

---

## 项目结构

```text
windows-clock/
├── WorldClockBar.sln
├── README.md
├── docs/ui-redesign/            # UI 设计规格与高保真预览
│   ├── design-proposals.md
│   └── mockups.html
└── src/WorldClockBar/
    ├── App.xaml(.cs)            # 入口、单实例、主题初始化
    ├── MainWindow.xaml(.cs)     # 时钟条主窗口（Acrylic 材质）
    ├── SettingsWindow.xaml(.cs) # 设置界面（NavigationView + 卡片，即时生效）
    ├── Styles/Fluent.xaml       # Fluent 控件样式库
    ├── Models/                  # 配置模型
    └── Services/
        ├── ThemeService.cs      # 深浅色/强调色检测、Mica/Acrylic 材质
        ├── SettingsService.cs   # JSON 读写
        ├── TimeDisplayService.cs  # 时区换算
        ├── WindowPlacementService.cs  # 定位 / 拖拽 / 边缘
        ├── AutostartService.cs    # 开机自启
        └── TrayIconService.cs     # 系统托盘
```

技术栈：**.NET 8 + WPF**（辅助使用 WinForms 的 `Screen` / 托盘 / 颜色对话框）。

**材质说明：** Win11 22H2+ 通过 `DwmSetWindowAttribute` 启用 Mica（设置窗口）与 Acrylic（时钟条，`SetWindowCompositionAttribute`，拖拽时自动切换为纯色防拖影）；Win10 自动降级为纯色半透明，功能不受影响。深浅色与强调色实时跟随系统。

---

## 常见问题

**Q：启动后看不到时钟条？**  
A：查看系统托盘是否有 WorldClockBar 图标 → 右键「显示时钟条」。或使用 `--reset` 恢复默认位置与样式。

**Q：时间准吗？要联网吗？**  
A：完全按本机系统时间换算，不联网。请保证 Windows 时间本身准确即可。

**Q：能塞进系统任务栏时钟区域吗？**  
A：本项目故意不注入任务栏，以降低与美化/插件的冲突。显示方式是贴在任务栏**上方**的独立条。

**Q：如何完全卸载？**  
A：退出程序 → 删除可执行文件目录 → 可选删除 `%AppData%\WorldClockBar` 与上述 Run 注册表项。

---

## 许可证

[MIT](LICENSE) — 可自由使用、修改与分发。

---

## 致谢

- Windows [`TimeZoneInfo`](https://learn.microsoft.com/dotnet/api/system.timezoneinfo) 提供夏令时与时区换算  
- 定位基于显示器 **WorkingArea**，避免侵入系统任务栏
