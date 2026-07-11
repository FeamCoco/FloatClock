# WorldClockBar

Windows 多时区时钟条：在**任务栏上方**常显其他国家/城市时间（时分秒），支持外观自定义、自由拖拽与边缘吸附。

> 不注入系统任务栏 / Explorer，尽量避免与第三方任务栏插件冲突。

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?logo=windows&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-green)

---

## 功能特性

| 功能 | 说明 |
|------|------|
| 多时区常显 | 横向显示多个城市，格式 `HH:mm:ss` |
| 本机换算 | 使用系统时钟 + `TimeZoneInfo`，**不访问网络** |
| 始终置顶 | 独立置顶窗口，定时巩固 Z 序，避免被普通窗口盖住 |
| 自由拖拽 | 可在**工作区**（任务栏以外区域）任意拖动 |
| 边缘吸附 | 靠近屏幕四边（含任务栏上沿）自动吸附，不会拖进任务栏 |
| 外观自定义 | 背景色 / 文字色 / 透明度 / 字体 / 高度 / 圆角，预设主题 |
| 多显示器 | 可选显示器；拖拽时可跨屏 |
| 开机自启 | 可选，写入当前用户 Run 注册表项 |
| 系统托盘 | 找不到窗口时可右键托盘「显示时钟条」 |
| 单实例 | 防止重复启动 |

**默认配置：** 英国（`GMT Standard Time`）· 白色背景 · 深色文字 · 右下角贴边

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

会恢复默认：英国时区 + 白色主题。

---

## 使用说明

| 操作 | 说明 |
|------|------|
| **左键拖动** | 在工作区内自由移动；靠近边缘自动吸附 |
| **双击** | 打开设置 |
| **右键** | 设置 / 选择显示器 / 开机自启 / 始终置顶 / 贴回右下角 / 退出 |
| **托盘图标** | 双击打开设置；右键可「显示时钟条」、退出 |
| **设置 → 城市/时区** | 添加、删除、排序城市，选择系统时区 |
| **设置 → 外观** | 颜色、字体、透明度、默认对齐与边距、主题预设 |
| **设置 → 行为** | 显示秒、开机自启等 |

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
    "background": "#F2FFFFFF",
    "foreground": "#FF111111",
    "fontSize": 14,
    "barHeight": 32,
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
| `freePosition` / `posX` / `posY` | 拖拽后的自由位置（相对工作区） |
| `horizontalAlignment` + `offsetX/Y` | 非自由位置时的贴边对齐 |
| `edgeSnapDistance` | 边缘吸附距离（DIP） |

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
└── src/WorldClockBar/
    ├── App.xaml(.cs)              # 入口、单实例
    ├── MainWindow.xaml(.cs)       # 时钟条主窗口
    ├── SettingsWindow.xaml(.cs)   # 设置界面
    ├── Models/                    # 配置模型
    └── Services/
        ├── SettingsService.cs     # JSON 读写
        ├── TimeDisplayService.cs  # 时区换算
        ├── WindowPlacementService.cs  # 定位 / 拖拽 / 边缘
        ├── AutostartService.cs    # 开机自启
        └── TrayIconService.cs     # 系统托盘
```

技术栈：**.NET 8 + WPF**（辅助使用 WinForms 的 `Screen` / 托盘 / 颜色对话框）。

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
