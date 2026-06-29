# Checklist

- [x] 1. `LRS/Models/ExtensionManifest.cs` 存在且包含 `Id` / `Name` / `Version` / `Type` / `Entry` / `Points` 字段
- [x] 2. `LRS/Models/ExtensionPoint.cs` 包含 4 个 `sealed class` 与 `ContextMenuApplyTo` 枚举
- [x] 3. `samples/example.hlds` 存在且覆盖 4 个扩展点（context_menu / topbar_buttons / file_columns / settings）
- [x] 4. 仓库根 `ext/.gitkeep` 存在，`ext/` 目录可被发现
- [x] 5. `LRS/Services/ExtensionLoader.cs` 永不抛异常，必填字段缺失 / `id` 含 `:` 时返回 `null` 并记日志
- [x] 6. `LRS/Services/ExtensionManager.cs` 是 `sealed class : IDisposable`
- [x] 7. `ExtensionManager` 暴露 `InitializeAsync` / `ReloadAsync` / `ExecuteAsync` / `GetContributions<T>` / `GetDisabledIds` / `SetEnabled` / `Changed`
- [x] 8. `ExtensionManager` 使用 `FileSystemWatcher` + 250ms `System.Timers.Timer` 防抖
- [x] 9. `App.xaml.cs` 在 DI 容器中注册 `ExtensionManager` 单例
- [x] 10. `App.xaml.cs` 在 `OnLaunched` 中 `await InitializeAsync` 并订阅 `Changed`
- [x] 11. `MiddleFilesView.xaml.cs` 中 `AppendExtensionContextMenuItems` 按 `ApplyTo` 过滤，点击通过 `Tag` 派发
- [x] 12. `TopView.xaml` 存在 `ExtensionButtonsHost`，`RebuildExtensionButtons` 按 `Position` 排序
- [x] 13. `TreeDataGrid.xaml.cs` 中 `ResolveExtensionCellValue` 静态映射 `length` / `modified` / `created` / `extension` / `name` / `path` / `isfile` / `type`
- [x] 14. `SettingsView.xaml` 存在「扩展 / Extensions」段，按 `SettingItem.Type` 渲染 ToggleSwitch / NumberBox / ComboBox / TextBox
- [x] 15. `Configs.cs` 含 `ExtensionsDisabledIds` (`ObservableCollection<string>`) 与 `_extensionSettings` (`Dictionary<string,string>`)
- [x] 16. `LRS/Configs/configs.json` 包含 `"Extensions": {"DisabledIds": [], "Settings": {}}` 段
- [x] 17. `README.md` 含第 6 章「扩展开发」且包含字段表 / 4 扩展点示例 / 占位符 / JSON 转义注意事项
- [x] 18. `LRS/LRS.csproj` 包含把 `..\ext\**\*.hlds` 拷贝到输出 `ext/` 的 `<None Include ... CopyToOutputDirectory="PreserveNewest" Link="ext\..." />` 项
