# LRS — LiveRootStorage

> 一款采用 Fluent 设计的开源、免费的 Windows 文件资源管理器。
> An open-source, free Windows file explorer with a Fluent design.

LRS 是一款基于 **WinUI 3 / Windows App SDK** 构建的现代化文件管理器，采用 MVVM 架构，提供贴近原生 Windows 资源管理器的使用体验，并支持通过注册表动态加载系统 Shell 右键菜单项。

LRS is a modern file manager built on **WinUI 3 / Windows App SDK**, following the MVVM pattern. It aims to deliver a familiar Explorer-like experience while integrating the system Shell context menu dynamically from the Windows registry.

---

## ✨ 特性 / Features

- 🎨 **Fluent UI 风格** — 基于 WinUI 3，遵循 Windows 11 设计语言
- 🌲 **双面板布局** — 左侧目录树，右侧文件列表，支持面包屑导航
- 📂 **基础文件操作** — 复制 / 剪切 / 粘贴 / 删除 / 重命名 / 新建文件夹 / 新建文本文档
- 🖱️ **完整右键菜单** — 文件列表、目录树与空白区域均提供上下文菜单
- 🔌 **Shell 集成** — 从 `HKEY_CLASSES_ROOT` 读取系统已注册的右键菜单项并动态注入
- ⚡ **两种图标获取方式** — `StorageFile.GetThumbnailAsync` 与 Win32 `SHGetFileInfo`（可配置）
- 🛠️ **配置热重载** — `configs.json` 变更后无需重启即可生效
- 📦 **解包 + 自包含** — `WindowsPackageType=None` + `WindowsAppSDKSelfContained=true`，无需 MSIX 即可分发

---

## 📑 目录 / Table of Contents

1. [下载与安装 / Download & Install](#1-下载与安装--download--install)
2. [从源码构建 / Build from Source](#2-从源码构建--build-from-source)
3. [项目结构 / Project Structure](#3-项目结构--project-structure)
4. [配置 / Configuration](#4-配置--configuration)
5. [开发说明 / Development Notes](#5-开发说明--development-notes)
6. [路线图 / Roadmap](#6-路线图--roadmap)
7. [许可证 / License](#7-许可证--license)

---

## 1. 下载与安装 / Download & Install

> 当前版本以 **解包（unpackaged）** 模式发布，直接运行可执行文件即可，无需安装证书。
> The current release ships as an **unpackaged** app. Just run the executable — no certificate required.

### 1.1 获取发布包 / Get the release

前往 [Releases](../../releases) 页面，下载对应架构的压缩包：

| 架构 / Arch | 适用设备 / Device             |
| ----------- | ------------------------------ |
| `x64`       | 大多数 Intel / AMD 桌面与笔记本 |
| `x86`       | 32 位系统                       |
| `ARM64`     | Snapdragon / ARM 设备            |

Download the archive matching your CPU architecture from the [Releases](../../releases) page.

### 1.2 运行 / Run

解压后直接双击 `LRS.exe` 即可启动。

Extract the archive and double-click `LRS.exe` to launch.

> ⚠️ 首次运行若被 SmartScreen 拦截，请在「更多信息 → 仍要运行」中放行。
> ⚠️ If SmartScreen blocks the app on first launch, click **More info → Run anyway**.

---

## 2. 从源码构建 / Build from Source

### 2.1 环境要求 / Prerequisites

| 工具 / Tool       | 版本 / Version                          |
| ----------------- | ---------------------------------------- |
| Visual Studio     | 2022 17.13 或更高（推荐 2026）           |
| Windows SDK       | 10.0.26100.0                             |
| .NET SDK          | 8.0.422+（由 `global.json` 锁定）        |
| Git for Windows   | 任意较新版本                              |

### 2.2 克隆 / Clone

```bash
git clone https://github.com/Xhscfdj/LiveRootStorage.git
cd LiveRootStorage
```

### 2.3 构建 / Build

```powershell
dotnet build
```

### 2.4 运行 / Run

```powershell
dotnet run --project LRS
```

或：在 Visual Studio 中打开 `LRS.slnx`，按 `F5` 启动。
Or: open `LRS.slnx` in Visual Studio and press `F5`.

> 启动配置文件位于 `LRS/Properties/launchSettings.json`：
>
> - **Project** — 解包模式（默认）
> - **MsixPackage** — 打包模式（如需 MSIX 调试）

---

## 3. 项目结构 / Project Structure

```
LRS/
├── App.xaml / App.xaml.cs       # 应用入口与 DI 容器
├── LRS.csproj                   # 项目文件（WinUI 3，net8.0-windows10.0.19041.0）
├── Package.appxmanifest         # MSIX 清单（解包模式下仅作资源声明）
├── Assets/                      # 图标、磁贴、徽标
├── Configs/                     # 运行时配置
│   ├── appSettings.json
│   └── configs.json
├── Models/                      # 数据模型
│   ├── LRSBreadcrumbModel.cs
│   └── ShellMenuItem.cs
├── Services/                    # 业务服务（DI 注入）
│   ├── FileOperator.cs          # 文件操作实现
│   ├── WindowsIconProvider.cs   # WinRT 缩略图
│   ├── ShellIconHelper.cs       # Win32 SHGetFileInfo + 缓存
│   └── ShellContextMenuService.cs # 注册表右键菜单读取
├── UserControls/                # 自定义控件
│   ├── LRSBreadcrumb.xaml(.cs)
│   ├── TreeDataGrid.xaml(.cs)
│   ├── ThemedIcon.xaml(.cs)
│   └── ...
├── ViewModels/                  # MVVM ViewModel 层
│   ├── MainWindowViewModel.cs   # 核心 ViewModel
│   ├── FileSystemNodeViewModel.cs
│   ├── Configs.cs               # 配置（支持热重载）
│   └── ...
└── Views/                       # XAML 页面
    ├── MainWindowView.xaml
    ├── FileTreeView.xaml(.cs)
    ├── MiddleFilesView.xaml(.cs) # 文件列表 + 右键菜单
    ├── TopView.xaml             # 顶部导航
    ├── SettingsView.xaml
    └── PinnedShortcuts.xaml
```

---

## 4. 配置 / Configuration

运行时配置位于 `LRS/Configs/configs.json`，**修改后会自动热重载**。

Runtime configuration lives in `LRS/Configs/configs.json` and **hot-reloads** on change.

| 键 / Key                       | 说明 / Description                          |
| ------------------------------ | -------------------------------------------- |
| `HomePageFullPath`             | 启动时默认打开的目录                          |
| `ifUsesWin32APIToGetIcon`      | 是否使用 Win32 `SHGetFileInfo` 提取图标       |
| `DefaultLanguage`              | 默认语言（`zh-Hans` / `en-US`）               |

> ⚠️ `Configs/appSettings.json` 当前为占位文件，请勿依赖。
> ⚠️ `appSettings.json` is a placeholder — do not rely on it.

---

## 5. 开发说明 / Development Notes

### 5.1 架构 / Architecture

- **MVVM** + `CommunityToolkit.Mvvm` 源生成器
  - `[ObservableProperty]` → 自动生成属性 + `OnXxxChanged`
  - `[RelayCommand]` → 自动生成 `ICommand`
- **DI 容器**：`Microsoft.Extensions.Hosting`（`App.xaml.cs`）
  - 注册：`Configs`、`IIconProvider`、`IFileOperator`、`ShellContextMenuService`
- **全局 ViewModel**：`App.SharedViewModel`

### 5.2 关键约束 / Key Constraints

| 项 / Item              | 说明 / Note                                                |
| ---------------------- | ----------------------------------------------------------- |
| `LangVersion`          | `preview` — 可使用最新 C# 特性                              |
| `Nullable`             | 启用 — 请处理所有可空引用                                   |
| `AllowUnsafeBlocks`    | `true`                                                      |
| `PublishTrimmed`       | Release 模式下启用，注意 trim 兼容性                         |
| UI 线程                | 所有 UI 更新必须通过 `DispatcherQueue`                       |
| 新增 XAML 文件         | 需在 `.csproj` 中添加 `<Page Update>` 条目                   |

### 5.3 Shell 右键菜单实现 / Shell Context Menu

`ShellContextMenuService` 负责从以下注册表路径读取菜单项，并按扩展名缓存：

- `HKEY_CLASSES_ROOT\*\shell` — 所有文件
- `HKEY_CLASSES_ROOT\<ext>\shell` — 特定扩展名
- `HKEY_CLASSES_ROOT\SystemFileAssociations\<ext>\shell`
- `HKEY_CLASSES_ROOT\Directory\shell` / `Folder\shell`
- `HKEY_CLASSES_ROOT\Directory\Background\shell` / `AllFilesystemObjects\Background\shell`

命令占位符 `%1` / `%L` / `%V` 会被替换为当前文件路径后通过 `Process.Start` 执行。

---

## 6. 路线图 / Roadmap

- [x] 基础文件浏览（目录树 + 文件列表 + 面包屑）
- [x] 系统 Shell 右键菜单动态集成
- [x] 配置热重载
- [x] 解包 + 自包含发布
- [ ] 多标签页 / Multi-tab browsing
- [ ] 文件预览面板 / File preview pane
- [ ] 暗色 / 亮色主题切换 / Theme switching
- [ ] 文件搜索增强 / Enhanced search
- [ ] 网络位置 / Network locations

---

## 7. 许可证 / License

本项目基于 [LICENSE.txt](./LICENSE.txt) 开源发布。

This project is released under the terms described in [LICENSE.txt](./LICENSE.txt).

---

<p align="center">
  如果你喜欢这个项目，欢迎 ⭐ Star！<br>
  If you find this project useful, please consider giving it a ⭐!
</p>
