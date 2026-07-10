# LRS-HungerCat — LiveRootStorage

> 一款采用 Fluent 设计的开源、免费的 Windows 文件资源管理器。
> An open-source, free Windows file explorer with a Fluent design.

LRS-HungerCat 是一款基于 **WinUI 3 / Windows App SDK** 构建的现代化文件管理器，采用 MVVM 架构，提供贴近原生 Windows 资源管理器的使用体验，并支持通过注册表动态加载系统 Shell 右键菜单项。

LRS-HungerCat is a modern file manager built on **WinUI 3 / Windows App SDK**, following the MVVM pattern. It aims to deliver a familiar Explorer-like experience while integrating the system Shell context menu dynamically from the Windows registry.

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
6. [扩展开发 / Extension Development](#6-扩展开发--extension-development)
7. [路线图 / Roadmap](#7-路线图--roadmap)
8. [许可证 / License](#8-许可证--license)

---

## 1. 下载与安装 / Download & Install

> 当前版本以 **解包（unpackaged）** 模式发布，直接运行可执行文件即可，无需安装证书。
> The current release ships as an **unpackaged** app. Just run the executable — no certificate required.

### 1.1 获取发布包 / Get the release

前往 [Releases](../../releases) 页面，下载EXE文件：

| 架构 / Arch | 适用设备 / Device             |
| ----------- | ------------------------------ |
| `x64`       | 大多数 Intel / AMD 桌面与笔记本 |

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
git clone https://github.com/Xhscfdj/LiveRootStorage.git](https://github.com/JOHNNY-beep456/LiveRootStorage-HungerCat.git
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

## 6. 扩展开发 / Extension Development

LRS 支持通过仓库根 `./ext/` 下的 `.hlds` 文件扩展右键菜单、顶栏按钮、文件列与设置项。**无需修改主程序源码，无需重新编译。**

LRS supports extending the right-click menu, top-bar buttons, file columns and settings via `.hlds` files under the repo-root `./ext/`. **No main-program changes or recompile required.**

### 6.1 扩展目录 / Extension directory

| 模式 / Mode | 实际路径 / Actual path                            |
| ----------- | -------------------------------------------------- |
| 开发 / Dev   | `<repo>/ext/*.hlds` （由 csproj 拷贝到输出）       |
| 部署 / Prod  | `AppContext.BaseDirectory/ext/*.hlds`              |

> 在设置页「扩展 / Extensions」段可查看实际扫描目录与加载状态。
> The settings page's *Extensions* section shows the actual scan directory and load state.

修改 `.hlds` 文件后，**250ms 内自动热重载**，无需重启 LRS。
After editing a `.hlds` file, LRS **hot-reloads within 250ms** — no restart needed.

### 6.2 `.hlds` 字段 / Fields

`.hlds` 是 JSON 文件。**`id` 不可包含 `:`**。

| 字段 / Field    | 类型 / Type | 必填 / Required | 说明 / Description |
| --------------- | ----------- | :-------------: | ------------------- |
| `id`            | string      | ✅              | 扩展唯一 ID，目录内不重复 |
| `name`          | string      | ✅              | 显示名 |
| `version`       | string      | ✅              | 语义化版本号（仅展示） |
| `type`          | string      | ✅              | 固定为 `"lrs-extension"` |
| `entry`         | string      | ✅              | 命令模板（见 §6.4 占位符） |
| `points`        | object      | ✅              | 4 个扩展点（见 §6.3） |
| `author`        | string      | ❌              | 作者 |
| `description`   | string      | ❌              | 描述 |

### 6.3 4 个扩展点 / 4 Extension Points

#### 6.3.1 `context_menu` — 右键菜单 / Right-click menu

```jsonc
"context_menu": [
  {
    "id": "open_with_notepad",   // 唯一命令 ID
    "label": "用记事本打开",     // 菜单显示文本
    "applyTo": "File",            // Any | File | Folder | Background
    "command": "notepad.exe \"%F\""
  }
]
```

- `applyTo: Any` — 任意位置都注入
- `applyTo: File` — 仅当右键目标是文件时
- `applyTo: Folder` — 仅当右键目标是文件夹时
- `applyTo: Background` — 仅当右键空白区域时

#### 6.3.2 `topbar_buttons` — 顶栏按钮 / Top-bar buttons

```jsonc
"topbar_buttons": [
  {
    "id": "git_pull",
    "label": "Git Pull",
    "command": "git.exe -C \"%D\" pull",
    "position": 50
  }
]
```

- `position` 越小越靠左；缺省时按扩展 `id` 字典序。

#### 6.3.3 `file_columns` — 文件表列 / File list columns

```jsonc
"file_columns": [
  {
    "id": "col_ext",
    "header": "扩展名",
    "value": "extension",       // 预定义表达式
    "width": 1.0
  }
]
```

`value` 必须是以下预定义表达式之一（**不支持**任意运行时表达式）：

| 表达式 / Expr | 输出 / Output                    |
| ------------- | --------------------------------- |
| `length`      | 文件大小（文件夹为空）             |
| `modified`    | 最后修改时间                      |
| `created`     | 创建时间                          |
| `extension`   | 文件扩展名（含点）                |
| `name`        | 名称                              |
| `path`        | 完整路径                          |
| `isfile`      | `"true"` / `"false"`             |
| `type`        | 节点类型名                        |

#### 6.3.4 `settings` — 设置项 / Extension settings

```jsonc
"settings": [
  {
    "key": "autoRefresh",     // 在设置页保存时存到 Extensions.Settings[<id>.<key>]
    "label": "操作后自动刷新",
    "type": "toggle",          // toggle | number | text | combo
    "default": "true"
  },
  {
    "key": "defaultEditor",
    "label": "默认编辑器",
    "type": "combo",
    "default": "notepad",
    "options": ["notepad", "code", "subl", "vim"]
  }
]
```

- `toggle` → 渲染为 `ToggleSwitch`（存 `"true"` / `"false"`）
- `number` → 渲染为 `NumberBox`
- `text` → 渲染为 `TextBox`
- `combo` → 渲染为 `ComboBox`（必须提供 `options`）

### 6.4 占位符 / Placeholders

命令模板（`entry` 与每个 contribution 的 `command` 字段）支持以下占位符：

| 占位符 / Placeholder | 替换为 / Replaced with                                |
| -------------------- | ------------------------------------------------------ |
| `%F`                 | 当前右键/选中的文件路径                                |
| `%D`                 | 当前目录                                              |
| `%L`                 | 多选时所有选中项的路径，分号 `;` 分隔                  |

### 6.5 JSON 转义注意 / JSON escaping

> ⚠️ Windows 路径分隔符是 `\` ，在 JSON 字符串中必须写成 `\\`。
> ⚠️ Windows path separators are `\` — in JSON strings, write `\\` instead.

✅ 正确 / Correct：
```json
"command": "notepad.exe \"C:\\Users\\me\\file.txt\""
```

❌ 错误 / Wrong：
```json
"command": "notepad.exe \"C:\Users\me\file.txt\""
```

### 6.6 调试 / Debugging

- 启动时单文件失败会写到 **Debug 输出**（`Output` 窗口 / `Debug.WriteLine`），**不阻塞**其他扩展加载（失败隔离）。
- 加载失败的扩展不会出现在设置页「扩展」列表中。
- 进程退出码非 0 时，`stdout` / `stderr` 同样写入 Debug 输出。
- 设置页「扩展」段下方展示扫描目录与已加载列表，便于核对。

### 6.7 完整示例 / Full example

参见 [`samples/example.hlds`](./samples/example.hlds)，覆盖全部 4 个扩展点。

---

## 7. 路线图 / Roadmap

- [x] 基础文件浏览（目录树 + 文件列表 + 面包屑）
- [x] 系统 Shell 右键菜单动态集成
- [x] 配置热重载
- [x] 解包 + 自包含发布
- [x] HLDS `.hlds` 扩展系统（右键菜单 / 顶栏 / 文件列 / 设置）
- [ ] 多标签页 / Multi-tab browsing
- [ ] 文件预览面板 / File preview pane
- [ ] 暗色 / 亮色主题切换 / Theme switching
- [ ] 文件搜索增强 / Enhanced search
- [ ] 网络位置 / Network locations

---

## 8. 许可证 / License

本项目基于 [LICENSE.txt](./LICENSE.txt) 开源发布。

This project is released under the terms described in [LICENSE.txt](./LICENSE.txt).

---

<p align="center">
  如果你喜欢这个项目，欢迎 ⭐ Star！<br>
  If you find this project useful, please consider giving it a ⭐!
</p>
