# HLDS 扩展系统（项目本地 `./ext`）Spec

## Why
LRS 是闭源 WinUI 3 文件管理器，用户无法修改功能 / 布局。通过在仓库根 `./ext/` 放置 `.hlds` 描述文件，让用户在不改主程序源码的前提下添加右键菜单项、顶栏按钮、文件列与设置项。扩展与代码一起入库，便于在团队内分发与版本管理。

## What Changes
- 新增 `.hlds` 描述文件格式（JSON 变体）。
- 新增扩展加载 / 调度服务 `ExtensionManager`（双目录扫描 + 热重载 + 失败隔离）。
- 新增 4 个扩展点贡献器：`ContextMenuContribution` / `TopbarButtonContribution` / `FileColumnContribution` / `SettingContribution`。
- 新增设置页中的「扩展」段：列出已加载扩展、支持启用 / 禁用、暴露扩展作者提供的设置项。
- 在 `README.md` 中新增第 6 章「扩展开发」。
- 在仓库根创建 `ext/` 目录（`.gitkeep` 占位），并把 `ext/` 下的 `*.hlds` 通过 csproj 拷贝到构建输出。
- 在 `samples/example.hlds` 中给出 4 个扩展点的最小示例。

## Impact
- Affected specs: 无（无既有相关规格）。
- Affected code:
  - 新增 `LRS/Models/ExtensionManifest.cs`
  - 新增 `LRS/Models/ExtensionPoint.cs`
  - 新增 `LRS/Services/ExtensionLoader.cs`
  - 新增 `LRS/Services/ExtensionManager.cs`
  - 新增 `samples/example.hlds`
  - 新增 `ext/.gitkeep`
  - 修改 `LRS/App.xaml.cs`（DI 注册 + 启动初始化 + Changed 订阅）
  - 修改 `LRS/ViewModels/MainWindowViewModel.cs`（通知属性变更 + 提供 ExtensionManager 访问）
  - 修改 `LRS/ViewModels/Configs.cs`（持久化 disabled 列表与扩展设置）
  - 修改 `LRS/Configs/configs.json`（新增 `Extensions` 段）
  - 修改 `LRS/Views/MiddleFilesView.xaml.cs`（追加右键菜单项 + ApplyTo 过滤）
  - 修改 `LRS/Views/TopView.xaml` + `TopView.xaml.cs`（顶栏按钮容器）
  - 修改 `LRS/UserControls/TreeDataGrid.xaml.cs`（文件列注册 + 单元值解析）
  - 修改 `LRS/Views/SettingsView.xaml` + `SettingsView.xaml.cs`（扩展段 + 设置项渲染）
  - 修改 `LRS/LRS.csproj`（把 `..\ext\**\*.hlds` 拷贝到输出 `ext/` 子目录）
  - 修改 `README.md`（新增第 6 章「扩展开发」，后续章节顺延编号）

## ADDED Requirements

### Requirement: `.hlds` 文件格式
系统 SHALL 接受仓库根 `./ext/` 下的 `*.hlds` 文件作为扩展描述文件。

`.hlds` 顶层 SHALL 包含：

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `id` | string | 是 | 扩展唯一 ID，不含 `:`，目录内不重复 |
| `name` | string | 是 | 显示名 |
| `version` | string | 是 | 语义化版本号（仅展示，不做版本仲裁） |
| `type` | string | 是 | 固定为 `"lrs-extension"` |
| `entry` | string | 是 | 命令模板，含 `%F` / `%D` / `%L` 占位符 |
| `points` | object | 是 | 键为扩展点名，值为该点贡献项数组 |
| `author` | string | 否 | 作者名 |
| `description` | string | 否 | 描述 |

#### Scenario: 加载成功
- **WHEN** 用户将合法 `.hlds` 放入 `./ext/`
- **THEN** 启动时（或热重载后）该扩展被加载、出现在设置页「扩展」列表中、其贡献项注入对应 UI

#### Scenario: 错误格式不阻塞
- **WHEN** 某个 `.hlds` JSON 解析失败或必填字段缺失 / `id` 含 `:`
- **THEN** 加载器记录到错误日志并跳过该文件，其他 `.hlds` 继续加载（失败隔离）

### Requirement: 4 个扩展点
系统 SHALL 实现以下 4 个扩展点。

#### Scenario: `context_menu`（右键菜单）
- **WHEN** 用户在文件 / 文件夹 / 空白处右键
- **THEN** 来自所有已加载扩展的 `points.context_menu` 项被追加到右键菜单
- 每个项的 `applyTo` 字段（`Any` / `File` / `Folder` / `Background`）过滤注入位置
- 点击时把 `%F` / `%D` / `%L` 替换为当前选中项 / 目录 / 分号分隔多选路径，再启动 `entry` 进程

#### Scenario: `topbar_buttons`（顶栏按钮）
- **WHEN** 应用启动 / 扩展热重载
- **THEN** 所有 `points.topbar_buttons` 项被追加到顶栏；按 `position` 升序，缺省时按扩展 `id` 字典序
- 点击行为同 `context_menu`

#### Scenario: `file_columns`（文件表列）
- **WHEN** 应用启动 / 扩展热重载
- **THEN** 所有 `points.file_columns` 项被注册为文件表的列
- 每列指定 `header` + `value`；`value` 是预定义表达式（`length` / `modified` / `created` / `extension` / `name` / `path` / `isfile` / `type`），**不支持** 任意运行时表达式

#### Scenario: `settings`（扩展设置项）
- **WHEN** 应用启动 / 扩展热重载
- **THEN** 设置页「扩展」段下出现扩展提供的设置项
- `SettingItem.type` 支持 `toggle` / `number` / `text` / `combo`
- 用户值保存到 `configs.json` 的 `Extensions.Settings[<id>]` 下，键为设置项 `key`

### Requirement: 扩展管理器（ExtensionManager）
系统 SHALL 提供一个 `ExtensionManager` 服务（`sealed class : IDisposable`）。

#### Scenario: 启动初始化
- **WHEN** 应用启动
- **THEN** 在 UI 线程上异步扫描 `<repo>/ext/`（开发）与 `AppContext.BaseDirectory/ext/`（部署）两个目录，加载所有合法 `.hlds`
- 失败隔离：单个 `.hlds` 出错不应导致整个初始化失败

#### Scenario: 热重载
- **WHEN** 任一被监控目录中文件被增 / 删 / 改
- **THEN** 250ms 防抖后重载并触发 `Changed` 事件；主程序订阅事件后刷新右键菜单 / 顶栏 / 列 / 设置 UI

#### Scenario: 命令执行
- **WHEN** 扩展被点击
- **THEN** 调用 `ExecuteAsync(string extensionId, string commandId, ExtensionContext ctx)`，`UseShellExecute=false`、`RedirectStandardError/Output=true`；返回 `record ExecutionResult(int ExitCode, string? Error)`

#### Scenario: 启用 / 禁用
- **WHEN** 用户在设置页切换扩展的启用状态
- **THEN** disabled ID 写入 `Configs.ExtensionsDisabledIds`，下次重载或初始化时跳过

### Requirement: 设置页扩展段
系统 SHALL 在设置页新增「扩展」段：
- 列出当前加载的所有扩展（name / version / author / description）
- 每行一个 `ToggleSwitch` 控制启用 / 禁用
- 按 `SettingItem.type` 渲染 `ToggleSwitch` / `NumberBox` / `TextBox` / `ComboBox`

#### Scenario: 用户操作
- **WHEN** 用户切换扩展开关或修改设置项
- **THEN** 立即写入 `Configs` 并触发 `ExtensionManager.Changed`，UI 同步刷新

### Requirement: 构建集成
`LRS.csproj` SHALL 把 `<repo>/ext/**` 下的 `*.hlds` 拷贝到构建输出 `ext/` 子目录（`PreserveNewest`），保证运行时的 `AppContext.BaseDirectory/ext/` 存在。

#### Scenario: 开发模式
- **WHEN** 用户在 VS / `dotnet run` 下启动
- **THEN** 运行时能读到 `<repo>/ext/*.hlds`

#### Scenario: 部署模式
- **WHEN** 应用以 unpackaged 模式运行
- **THEN** 运行时能读到 `AppContext.BaseDirectory/ext/*.hlds`

### Requirement: README 扩展开发文档
`README.md` SHALL 新增第 6 章「扩展开发」，包含：
- `.hlds` 字段表
- 4 个扩展点各一个最小示例
- 占位符说明（`%F` / `%D` / `%L`）
- JSON 转义注意事项（Windows 路径中的反斜杠必须用 `\\`）
- 调试日志位置（设置页错误列表 + 日志文件）

#### Scenario: 文档完整
- **WHEN** 第三方开发者阅读 README 第 6 章
- **THEN** 能在不看源码的情况下写出可工作的最小 `.hlds`

## MODIFIED Requirements
（无）

## REMOVED Requirements
（无）
