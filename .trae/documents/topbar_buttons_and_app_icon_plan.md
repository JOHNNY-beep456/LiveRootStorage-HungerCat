# 顶部「新建文件夹 / 新建文件」按钮 + 应用图标切换为 tb-tm.png

## 摘要
1. 把用户上传的 `tb-tm.png`（猫图）落地到 `LRS/Assets/`，替换 TitleBar 标题栏图标、Package.appxmanifest 的所有打包图标引用，并保证 Win32 单文件分发也能显示新图标。
2. 在 [TopView.xaml](file:///workspace/LRS/Views/TopView.xaml) 面包屑右侧新增两个 `AppBarButton`：「新建文件夹」和「新建文件」。
3. 「新建文件」按钮根据新增的 `Configs.NewFileMode` 配置走两种行为：默认弹出 ContentDialog 提供预设扩展名（.txt / .md / .docx / .xlsx / .pptx / .json / .csv），用户可在设置里切换为「输入扩展名」模式。设置卡片中显示提示语告知用户可切换。

---

## 当前状态分析

### 图标现状
- 标题栏图标：[MainWindowView.xaml#L27-L33](file:///workspace/LRS/Views/MainWindowView.xaml#L27-L33) 使用的 `<FontIconSource Glyph="&#xE838;"/>`（Segoe MDL2 字体字符）。
- 包清单图标：[Package.appxmanifest#L7-L23](file:///workspace/LRS/Package.appxmanifest#L7-L23) 引用了 `Assets\StoreLogo.png` / `Square150x150Logo.png` / `Square44x44Logo.png` / `Wide310x150Logo.png` / `SmallTile.png` / `LargeTile.png` / `SplashScreen.png` / `BadgeLogo.png`。
- 工程下 `LRS/Assets/` 已有多套 scale 的 PNG（见 Phase 1 LS 列表），但 `tb-tm.png` 尚未存在。

### 顶部栏现状
- [TopView.xaml](file:///workspace/LRS/Views/TopView.xaml) 当前只有一个 `<LRSCtrls:LRSBreadcrumb>`，外层为 `Grid`。
- [TopView.xaml.cs](file:///workspace/LRS/Views/TopView.xaml.cs) 直接用 `App.SharedViewModel` 作为 DataContext，订阅 `BreadcrumbRefreshRequested` 事件。
- 现有按钮命令：[MainWindowViewModel.cs#L189-L205](file:///workspace/LRS/ViewModels/MainWindowViewModel.cs#L189-L205) 已存在 `NewFolderCommand` 和 `NewTextDocumentCommand`（后者是写死 .txt 文本文件）。

### 设置与配置现状
- 配置类：[Configs.cs](file:///workspace/LRS/ViewModels/Configs.cs) 使用 `[ObservableProperty]` 源生成器，按段读 `configuration.GetValue("Section:Key", defaultValue)`，保存走手写 JSON 字符串。
- 设置面板：[SettingsView.xaml](file:///workspace/LRS/Views/SettingsView.xaml) 使用 `CommunityToolkit.WinUI.Controls.SettingsCard / SettingsExpander`，带标题小节（通用 / 外观 / 高级 / 性能 / 调试）。
- 现有保存逻辑：[SettingsView.xaml.cs#L18-L21](file:///workspace/LRS/Views/SettingsView.xaml.cs#L18-L21) 由底部"保存设置"按钮触发 `AppConfigs.SaveConfig()`。
- [configs.json](file:///workspace/LRS/Configs/configs.json) 内容极简，仅占位。

---

## 实施方案

### 一、准备 tb-tm.png 图标资源

1. 用户已上传 `tb-tm.png`（在对话中）。需要把图片保存到 `LRS/Assets/tb-tm.png` 作为源文件。
2. 标题栏图标使用同一文件 `tb-tm.png`（运行期 `ms-appx:///Assets/tb-tm.png`）。
3. 打包清单图标继续使用 `tb-tm.png` 一个源文件：
   - 在 [LRS.csproj](file:///workspace/LRS/LRS.csproj) 的 `<Content>` 列表中新增 `Assets\tb-tm.png`（`CopyToOutputDirectory` 行为按 MSBuild 默认即可，UWP/WinUI 类资源用 Page 或 None+CopyToOutputDirectory 都行；为避免引入新的 PRI 警告，按现有 Content 模式）。
   - 删除 / 替换 `Package.appxmanifest` 中对 `StoreLogo.png` / `Square44x44Logo.png` / `Square150x150Logo.png` / `Wide310x150Logo.png` / `SmallTile.png` / `LargeTile.png` / `SplashScreen.png` / `BadgeLogo.png` 的引用，全部改为 `Assets\tb-tm.png`（一个源文件喂所有清单槽位）。注意 Windows 商店打包会对这些槽位有最低尺寸要求，但当前工程是 `WindowsPackageType=None` 单文件分发，不走商店，清单仍会生成用于运行时（`App.xaml.cs` 中有 `Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", ...)` 自承载）。把全部槽位统一指向 `tb-tm.png` 可以避免因尺寸不匹配产生新的 PRI 警告并保持单一图标。
4. 工程中现有的 `Assets/*.scale-200.png` 等大量多 scale 文件**保留**不动，避免引入 PRI 警告回归；只在 manifest 中替换引用即可。
5. 标题栏图标（运行期显示）：
   - 将 [MainWindowView.xaml#L30-L32](file:///workspace/LRS/Views/MainWindowView.xaml#L30-L32) 的 `<FontIconSource Glyph="&#xE838;"/>` 替换为 `<BitmapIconSource UriSource="ms-appx:///Assets/tb-tm.png" ShowAsMonochrome="False"/>`（`ShowAsMonochrome=False` 让彩色猫图显示在标题栏上；如需在浅色/深色主题下自动取单色版本则可改为 `True`，但用户上传的是彩色图，所以默认 `False`）。

### 二、新建文件夹 / 新建文件按钮

1. 修改 [TopView.xaml](file:///workspace/LRS/Views/TopView.xaml)：
   - 把外层 `<Grid>` 改为两列布局：左列放 `<LRSBreadcrumb>`（保持原有绑定 / Margin / Height），右列放 `StackPanel Orientation="Horizontal"` 包含两个 `AppBarButton`。
   - 按钮：
     - `AppBarButton` 绑定到 `NewFolderCommand`，`Label="新建文件夹"`，`Icon` 用 `FontIcon Glyph="&#xE8B7;"`（新建文件夹的 Segoe MDL2 图标）。
     - `AppBarButton` 绑定到 `NewFileCommand`，`Label="新建文件"`，`Icon` 用 `FontIcon Glyph="&#xE7C3;"`（新建文件 / 页面图标）。
   - 列宽：第一列 `*`，第二列 `Auto`；面包屑 Margin 从 `10,0,10,0` 改为 `10,0,0,0`，右列加 `Margin="8,0,10,0"`。

2. 在 [MainWindowViewModel.cs](file:///workspace/LRS/ViewModels/MainWindowViewModel.cs)：
   - 保留并复用现有 `NewFolderCommand`（已是 `[RelayCommand] NewFolder`，按钮直接绑它）。
   - 新增 `NewFileCommand`（`[RelayCommand] private async Task NewFile()`）：
     - 取 `AppConfigs.NewFileMode` 判断行为：
       - `Preset`（默认）：调用新增的 `ShowNewFilePresetDialogAsync()`，根据用户在 ContentDialog 中选择的预设项创建文件，文件名 `新建{DisplayName}{Ext}`，路径走 `SelectedFolder.FullPath ?? CurrentBreadcrumbPath`，文件名用现有 `GenerateUniquePath` 避免冲突。
       - `InputExtension`：调用新增的 `ShowNewFileInputDialogAsync()`，让用户输入带 `.` 的扩展名（默认 `.txt`），校验格式后创建 `新建文件{ext}`。
     - 完成后 `await RefreshCurrentFolder()`。
   - 两个 dialog 方法放在 MainWindowViewModel 内部，因为只有它能拿到 `AppConfigs` 与当前目录；它们通过 `App.CurrentWindow` 或 `App.SharedWindow` 拿到 `XamlRoot`（与现有 ContentDialog 调用方式保持一致；如 MainWindowView.xaml.cs 已存在 `this.Content.XamlRoot` 暴露则用之，若无则用 `App.MainWindowInstance.Content.XamlRoot`）。为最小化改动，将 dialog 写在 MainWindowViewModel 内并通过 `App.MainWindow?.Content.XamlRoot` 获取（如果该静态属性不存在则新增一个 `public static Window? MainWindow` 在 `App.xaml.cs`）。
   - 新增 `NewFilePresets` 静态只读列表：`.txt` "文本文档" / `.md` "Markdown 文档" / `.docx` "Word 文档" / `.xlsx` "Excel 工作簿" / `.pptx` "PowerPoint 演示文稿" / `.json` "JSON 文件" / `.csv` "CSV 文件"。

3. 在 [App.xaml.cs](file:///workspace/LRS/App.xaml.cs)：
   - 新增 `public static Microsoft.UI.Xaml.Window? MainWindowInstance { get; private set; }`，在 `OnLaunched` 创建 `MainWindowView` 之后赋值给该静态属性，供 ViewModel 拿到 `XamlRoot`。

### 三、配置项 NewFileMode

1. [Configs.cs](file:///workspace/LRS/ViewModels/Configs.cs)：
   - 新增枚举：
     ```csharp
     public enum NewFileMode { Preset, InputExtension }
     ```
   - 新增字段：
     ```csharp
     [ObservableProperty] private NewFileMode _newFileMode = NewFileMode.Preset;
     ```
   - `ReadConfigs()` 末尾加：
     ```csharp
     var modeStr = configuration.GetValue("General:NewFileMode", "Preset")!;
     NewFileMode = Enum.TryParse<NewFileMode>(modeStr, true, out var m) ? m : NewFileMode.Preset;
     ```
   - `SaveConfig()` 的 `General` 段补一行：
     ```csharp
     $"    \"NewFileMode\": \"{NewFileMode}\"\n"
     ```
   - （顺带把 `DefaultOrderMode` 与新加的 `NewFileMode` 的 `,` 拼接保持 JSON 合法——`DefaultOrderMode` 那一行末尾要保留 `,`。）

2. [SettingsView.xaml](file:///workspace/LRS/Views/SettingsView.xaml)：
   - 在「通用」小节末尾、`AppConfigs.HomePageFullPath` 卡之后新增一个 `SettingsCard`：
     - `Header="新建文件行为"`
     - 内部：一个 `StackPanel Orientation="Vertical" Spacing="4"`，上面 `ComboBox`（`ItemsSource="{Binding NewFileModePairs}" DisplayMemberPath="Value" SelectedValuePath="Key"`，`SelectedValue` 双向绑定到 `AppConfigs.NewFileMode`），下面一个 `TextBlock Foreground="{ThemeResource TextFillColorSecondaryBrush}" Text="默认弹出预设菜单（.txt/.md/.docx 等）。切换为「输入扩展名」后，新建文件按钮将弹出文本框。"`。
   - 同时为已有 `OrderModeComboBox` 已有的写法保持一致。

3. [SettingsViewModel.cs](file:///workspace/LRS/ViewModels/SettingsViewModel.cs)：
   - 新增静态字典：
     ```csharp
     public static List<KeyValuePair<NewFileMode, string>> NewFileModePairs { get; } =
         new Dictionary<NewFileMode, string>
         {
             { NewFileMode.Preset, "预设扩展名" },
             { NewFileMode.InputExtension, "输入扩展名" }
         }.ToList();
     ```

### 四、文件操作复用
- 已存在的 `GenerateUniquePath`（[MainWindowViewModel.cs#L107-L126](file:///workspace/LRS/ViewModels/MainWindowViewModel.cs#L107-L126)）直接复用做命名冲突处理。
- 现有 `NewTextDocumentCommand`（写死 `.txt`）保留，作为 `Preset` 模式下 `.txt` 路径的备选实现逻辑样板（结构相同，只是参数化扩展名）。

### 五、csproj 微调
- 在 [LRS.csproj](file:///workspace/LRS/LRS.csproj) 的 `<Content>` 段增加：
  ```xml
  <Content Include="Assets\tb-tm.png" />
  ```
  让它被发布/单文件复制到输出目录（`TitleBar` 的 `BitmapIconSource` 运行时需要 `ms-appx:///Assets/tb-tm.png` 能解析）。

---

## 关键文件改动一览

| 文件 | 变更 |
|------|------|
| `LRS/Assets/tb-tm.png` | **新增**（用户提供的图标原图） |
| `LRS/Views/MainWindowView.xaml` | `FontIconSource` → `BitmapIconSource ms-appx:///Assets/tb-tm.png` |
| `LRS/Package.appxmanifest` | 8 个图标引用统一改为 `Assets\tb-tm.png` |
| `LRS/LRS.csproj` | 新增 `<Content Include="Assets\tb-tm.png"/>` |
| `LRS/Views/TopView.xaml` | Grid 双列：左 LRSBreadcrumb，右两个 AppBarButton |
| `LRS/ViewModels/MainWindowViewModel.cs` | 新增 `NewFileCommand`、`NewFilePresets`、两个 dialog 助手方法 |
| `LRS/App.xaml.cs` | 新增 `public static Window? MainWindowInstance` 供 VM 取 `XamlRoot` |
| `LRS/ViewModels/Configs.cs` | 新增 `NewFileMode` 枚举与字段，读 / 写配置 |
| `LRS/ViewModels/SettingsViewModel.cs` | 新增 `NewFileModePairs` 静态字典 |
| `LRS/Views/SettingsView.xaml` | 通用小节加「新建文件行为」SettingsCard + 提示语 |

---

## 假设与决策
- tb-tm.png 是彩色图，标题栏彩色显示（`ShowAsMonochrome=False`）。
- 工程目前是 `WindowsPackageType=None` 单文件分发，不走商店，因此可让所有 manifest 槽位都引用同一 `tb-tm.png`。
- 「新建文件」默认走 ContentDialog 弹出预设扩展名（.txt / .md / .docx / .xlsx / .pptx / .json / .csv），用户可在设置里切换为「输入扩展名」模式。
- 按钮样式沿用项目风格 `AppBarButton + FontIcon`（与现有 CommandBarFlyout 一致），不使用第三方控件。
- 不新增依赖、不修改 `Configs.SaveConfig` 的 JSON 序列化方式（保持手写字符串拼接，只追加新键）。

---

## 验证步骤
1. `dotnet build`：
   - 期望：构建通过，**不**新增警告（`tb-tm.png` 不会触发新的 PRI 警告，因为它是新 Content，文件存在）。
   - 预期会出现的原 Pri 警告已在现有 `NoWarn` 列表中抑制。
2. 启动应用 `dotnet run --project LRS`：
   - 标题栏左侧出现猫图。
   - 顶部面包屑右侧出现「新建文件夹 / 新建文件」两个按钮。
   - 打开任意文件夹 → 点「新建文件夹」→ 当前目录出现 `新建文件夹`，再次点击自动加 ` (2)`。
   - 点「新建文件」→ 弹出 ContentDialog 含 7 个预设扩展名 → 选「Markdown 文档」→ 当前目录出现 `新建Markdown文档.md`。
   - 打开设置 → 通用 → 「新建文件行为」切换到「输入扩展名」→ 保存 → 再点「新建文件」→ 弹出输入扩展名对话框 → 输入 `.log` → 当前目录出现 `新建文件.log`。
3. `dotnet publish -c Release -r win-x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:PublishSingleFile=true` 验证单文件产物仍能显示新图标。
4. `git diff` 检查 `LRS/Package.appxmanifest` 与 `LRS/LRS.csproj` 中没有产生未预期的回退。
