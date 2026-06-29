# Tasks

- [x] Task 1: 设计 `.hlds` 格式 + 模型
  - [x] SubTask 1.1: 创建 `LRS/Models/ExtensionManifest.cs`（POCO，含 `Id` / `Name` / `Version` / `Type` / `Entry` / `Points` / `Author` / `Description`）
  - [x] SubTask 1.2: 创建 `LRS/Models/ExtensionPoint.cs`（`ContextMenuApplyTo` 枚举 + `ContextMenuContribution` / `TopbarButtonContribution` / `FileColumnContribution` / `SettingContribution` 四个 `sealed class`，含 `[JsonPropertyName]`）
  - [x] SubTask 1.3: 创建 `samples/example.hlds`，覆盖 4 个扩展点
  - [x] SubTask 1.4: 在仓库根创建 `ext/.gitkeep`

- [x] Task 2: 实现 `ExtensionLoader`
  - [x] SubTask 2.1: `LRS/Services/ExtensionLoader.cs` — 静态 `LoadFromFile(string path)`，使用 `System.Text.Json`，**永不抛**，校验 `id` / `name` / `version` / `type` 与 `id` 不含 `:`
  - [x] SubTask 2.2: 占位符替换工具方法 `SubstitutePlaceholders(string template, ExtensionContext ctx)`

- [x] Task 3: 实现 `ExtensionManager` 服务
  - [x] SubTask 3.1: `LRS/Services/ExtensionManager.cs` — `sealed class : IDisposable`
  - [x] SubTask 3.2: 加载源：`<repo-root>/ext/`（开发）+ `AppContext.BaseDirectory/ext/`（部署），并集去重
  - [x] SubTask 3.3: 公共 API：`InitializeAsync(CancellationToken)` / `ReloadAsync()` / `ExecuteAsync(string extensionId, string commandId, ExtensionContext)` / `GetContributions<T>()` / `GetDisabledIds()` / `SetEnabled(string, bool)` / `event EventHandler? Changed`
  - [x] SubTask 3.4: 内部 `record ExecutionResult(int ExitCode, string? Error)`
  - [x] SubTask 3.5: 双目录 `FileSystemWatcher` + `System.Timers.Timer` 250ms 防抖；`Changed` 事件 marshal 回 UI 线程（`DispatcherQueue`）

- [x] Task 4: 接入 App 启动与 DI
  - [x] SubTask 4.1: `App.xaml.cs` 中 `services.AddSingleton<ExtensionManager>()`
  - [x] SubTask 4.2: `OnLaunched` 中 `await InitializeAsync`（1.5s `CancellationToken`）
  - [x] SubTask 4.3: 订阅 `Changed` → `vm.OnExtensionsChanged()`（在 UI 线程）
  - [x] SubTask 4.4: `MainWindowViewModel` 提供 `App.Services` 懒解析的 `ExtensionManager` 属性

- [x] Task 5: 4 个扩展点贡献器接入 UI
  - [x] SubTask 5.1: `MiddleFilesView.xaml.cs` — `AppendExtensionContextMenuItems` 含 `ApplyTo` 过滤；`OnExtensionMenuItemClick` 通过 `Tag = "extension_dynamic:<extId>:<commandId>"` 派发
  - [x] SubTask 5.2: `TopView.xaml` + `TopView.xaml.cs` — 新增 `<ItemsControl x:Name="ExtensionButtonsHost">`；`RebuildExtensionButtons` 按 `Position` 然后 `Id` 排序
  - [x] SubTask 5.3: `TreeDataGrid.xaml.cs` — 新增 `SetExtensionColumns(IReadOnlyList<FileColumn>)` + 静态 `ResolveExtensionCellValue` 映射预定义表达式
  - [x] SubTask 5.4: `SettingsView.xaml` + `SettingsView.xaml.cs` — 新增「扩展 / Extensions」段；`BuildExtensionsSection` 按 `SettingItem.Type` 渲染 `ToggleSwitch` / `NumberBox` / `ComboBox` / `TextBox`

- [x] Task 6: Configs 持久化
  - [x] SubTask 6.1: `Configs.cs` 新增 `[ObservableProperty] ObservableCollection<string> _extensionsDisabledIds`
  - [x] SubTask 6.2: `Dictionary<string, string> _extensionSettings` + `GetExtensionSetting(extensionId, key, fallback)` / `SetExtensionSetting`
  - [x] SubTask 6.3: `SaveConfig` 改用 `JsonSerializer.Serialize(Dictionary<string, object?>, WriteIndented=true)`，输出 `Extensions` 段
  - [x] SubTask 6.4: `LRS/Configs/configs.json` 新增 `"Extensions": {"DisabledIds": [], "Settings": {}}` 段

- [x] Task 7: README 扩展开发文档
  - [x] SubTask 7.1: `README.md` 新增第 6 章「扩展开发」（字段表 / 4 扩展点示例 / 占位符 / JSON 转义 / 调试日志）
  - [x] SubTask 7.2: 后续章节顺延编号

- [x] Task 8: 构建集成
  - [x] SubTask 8.1: `LRS/LRS.csproj` 加入 `<None Include="..\ext\**\*.hlds" CopyToOutputDirectory="PreserveNewest" Link="ext\%(RecursiveDir)%(Filename)%(Extension)" />`

- [x] Task 9: 验证
  - [x] SubTask 9.1: 源码级 review 18 项 checklist
  - [ ] SubTask 9.2: （DEFERRED）Windows 主机 `dotnet build` + 端到端验证四项扩展点
  - [ ] SubTask 9.3: （DEFERRED）Windows 主机：非法 `.hlds` 触发失败隔离验证

# Task Dependencies
- [Task 3] depends on [Task 1, Task 2]
- [Task 4] depends on [Task 3]
- [Task 5] depends on [Task 3, Task 4]
- [Task 6] depends on [Task 4]
- [Task 7] depends on [Task 1]
- [Task 8] depends on [Task 1]
- [Task 9] depends on [Task 1, Task 2, Task 3, Task 4, Task 5, Task 6, Task 7, Task 8]
