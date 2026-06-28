# 新建交互：自定义文件名 + 重名提示 + 创建后自动选中

## 摘要
1. 「新建文件夹 / 新建文件」按钮改为**先弹 ContentDialog 输入名称**（默认名预填且全选）。
2. 若用户输入的名称（含扩展名）与目标目录下的现有项冲突（大小写不敏感），在对话框内用 `InfoBar` 显示「同名已存在，无法创建」错误，**不关闭对话框**，光标留在输入框。
3. 创建成功后：刷新 `CurrentFolderContent` → 找到新项 → 通过事件通知 View 把 `FileListView.SelectedItem` 设为该项。

---

## 当前状态分析

- [TopView.xaml](file:///workspace/LRS/Views/TopView.xaml) 已有「新建文件夹」Button + 「新建文件」Button（带 MenuFlyout，7 个预设 + 「输入扩展名...」）。
- [TopView.xaml.cs](file:///workspace/LRS/Views/TopView.xaml.cs) `OnNewFolderClick` 直接 `NewFolderCommand.Execute(null)`；`OnNewFilePresetClick` 直接调 `NewFileWithPresetAsync` / `NewFileWithInputExtensionAsync`。
- [MainWindowViewModel.cs](file:///workspace/LRS/ViewModels/MainWindowViewModel.cs#L191-L298) 当前 `NewFolder` / `NewFileWithPresetAsync` / `NewFileWithInputExtensionAsync` 都使用 `GenerateUniquePath` 静默加 ` (2)` 后缀，不报错。
- [UserControls/TreeDataGrid.xaml.cs:467-468](file:///workspace/LRS/UserControls/TreeDataGrid.xaml.cs#L467-L468) 右键时会设置 `SelectedItem` 和 `FileListView.SelectedItem`，但没有从外部 selected 的接口。
- `MainWindowViewModel.CurrentFolderContent` 是 `ObservableCollection<FileSystemNodeViewModel>`，每项 `FullPath` 可作为查找 key。
- `FileSystemNodeViewModel` 有 `IsSelected` 字段（用于 grid 中的选中样式）和 `IsRenaming` 字段（用于就地重命名）。

---

## 实施方案

### 一、新建 ContentDialog（输入名 + 实时重名检测）

在 `MainWindowViewModel.cs` 新增：
```csharp
public record NewItemRequest(bool IsFolder, string? Extension, string DefaultName);
public record NewItemResult(string Name, string FullPath);

public async Task<NewItemResult?> ShowNewItemDialogAsync(NewItemRequest req)
{
    var xamlRoot = (App.MainWindow?.Content as FrameworkElement)?.XamlRoot;
    if (xamlRoot == null) return null;

    var nameBox = new TextBox
    {
        Text = req.DefaultName,
        PlaceholderText = req.IsFolder ? "文件夹名" : "文件名",
        SelectAllOnFocus = true,
    };
    var extLabel = req.IsFolder ? null : new TextBlock { Text = req.Extension ?? "", VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(4, 0, 0, 8) };
    var errorBar = new InfoBar
    {
        IsOpen = false,
        Severity = InfoBarSeverity.Error,
        Title = "无法创建",
        Message = "同名文件或文件夹已存在。",
    };

    var inputRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };
    inputRow.Children.Add(nameBox);
    if (extLabel != null) inputRow.Children.Add(extLabel);

    var stack = new StackPanel { Spacing = 8, MinWidth = 360 };
    stack.Children.Add(inputRow);
    stack.Children.Add(errorBar);

    var dlg = new ContentDialog
    {
        Title = req.IsFolder ? "新建文件夹" : "新建文件",
        Content = stack,
        PrimaryButtonText = "创建",
        CloseButtonText = "取消",
        DefaultButton = ContentDialogButton.Primary,
        XamlRoot = xamlRoot,
    };

    nameBox.TextChanged += (_, _) => errorBar.IsOpen = false;
    dlg.PrimaryButtonClick += async (s, args) =>
    {
        var name = nameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) { args.Cancel = true; return; }
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { ShowError("名称包含非法字符。"); args.Cancel = true; return; }
        if (name.EndsWith(".") || name.EndsWith(" ")) { ShowError("名称不能以点或空格结尾。"); args.Cancel = true; return; }

        var fullName = req.IsFolder ? name : $"{name}{req.Extension}";
        var destDir = SelectedFolder?.FullPath ?? CurrentBreadcrumbPath;
        var newPath = Path.Combine(destDir, fullName);
        if (File.Exists(newPath) || Directory.Exists(newPath))
        {
            ShowError($"已存在同名项：{fullName}");
            args.Cancel = true;
            return;
        }
        // 标记已校验，关闭对话框
        _lastValidated = new NewItemResult(name, newPath);
    };

    var result = await dlg.ShowAsync();
    return result == ContentDialogResult.Primary ? _lastValidated : null;

    void ShowError(string msg)
    {
        errorBar.Message = msg;
        errorBar.IsOpen = true;
    }
}
private NewItemResult? _lastValidated;
```

### 二、把现有 `NewFolder` / `NewFile*` 改造为「输入 → 创建 → 选中」三段

```csharp
public async Task NewFolder()
{
    var destDir = SelectedFolder?.FullPath ?? CurrentBreadcrumbPath;
    var req = new NewItemRequest(IsFolder: true, Extension: null, DefaultName: "新建文件夹");
    var r = await ShowNewItemDialogAsync(req);
    if (r == null) return;
    Directory.CreateDirectory(r.FullPath);
    await RefreshAndSelectAsync(r.FullPath);
}

public async Task NewFileWithPresetAsync(NewFilePreset preset)
{
    var req = new NewItemRequest(IsFolder: false, Extension: preset.Extension,
        DefaultName: preset.DisplayName);
    var r = await ShowNewItemDialogAsync(req);
    if (r == null) return;
    File.Create(r.FullPath).Dispose();
    await RefreshAndSelectAsync(r.FullPath);
}

public async Task NewFileWithInputExtensionAsync()
{
    var xamlRoot = (App.MainWindow?.Content as FrameworkElement)?.XamlRoot;
    if (xamlRoot == null) return;
    var extBox = new TextBox { Text = ".txt", PlaceholderText = ".扩展名" };
    var extDlg = new ContentDialog
    {
        Title = "新建文件",
        Content = extBox,
        PrimaryButtonText = "下一步",
        CloseButtonText = "取消",
        XamlRoot = xamlRoot,
    };
    if (await extDlg.ShowAsync() != ContentDialogResult.Primary) return;
    var ext = extBox.Text.Trim();
    if (string.IsNullOrEmpty(ext)) return;
    if (!ext.StartsWith(".")) ext = "." + ext;

    var req = new NewItemRequest(IsFolder: false, Extension: ext, DefaultName: "新建文件");
    var r = await ShowNewItemDialogAsync(req);
    if (r == null) return;
    File.Create(r.FullPath).Dispose();
    await RefreshAndSelectAsync(r.FullPath);
}
```

### 三、刷新并选中

```csharp
public event Action<FileSystemNodeViewModel?>? RequestSelectItem;

private async Task RefreshAndSelectAsync(string newPath)
{
    await RefreshCurrentFolder();
    var item = CurrentFolderContent.FirstOrDefault(
        n => string.Equals(n.FullPath, newPath, StringComparison.OrdinalIgnoreCase));
    RequestSelectItem?.Invoke(item);
}
```

### 四、View 端订阅选中事件

`MiddleFilesView.xaml.cs` 已持有 FileGrid 实例。在 `OnNavigatedTo` / 构造里订阅：
```csharp
App.SharedViewModel.RequestSelectItem += OnRequestSelectItem;

private void OnRequestSelectItem(FileSystemNodeViewModel? item)
{
    if (item == null) return;
    DispatcherQueue.TryEnqueue(() =>
    {
        FileGrid.FileListView.SelectedItem = item;
        FileGrid.ScrollIntoView(item);
    });
}
```

并在 `OnNavigatedFrom` / 卸载时 `-= OnRequestSelectItem` 防泄漏。

### 五、TopView 入口改造

[TopView.xaml.cs](file:///workspace/LRS/Views/TopView.xaml.cs) 中：
```csharp
private async void OnNewFolderClick(object sender, RoutedEventArgs e)
{
    await App.SharedViewModel.NewFolder();
}

private async void OnNewFilePresetClick(object sender, RoutedEventArgs e)
{
    if (sender is not MenuFlyoutItem mfi) return;
    try
    {
        if (mfi.Tag is string s && s == "INPUT")
            await App.SharedViewModel.NewFileWithInputExtensionAsync();
        else if (mfi.Tag is MainWindowViewModel.NewFilePreset p)
            await App.SharedViewModel.NewFileWithPresetAsync(p);
    }
    catch (Exception ex) { Debug.WriteLine($"[TopView] {ex}"); }
}
```

---

## 关键文件改动一览

| 文件 | 变更 |
|------|------|
| `LRS/ViewModels/MainWindowViewModel.cs` | `NewFolder` / `NewFileWithPresetAsync` / `NewFileWithInputExtensionAsync` 改为弹 dialog 流程；新增 `ShowNewItemDialogAsync` / `NewItemRequest` / `NewItemResult` / `RefreshAndSelectAsync` / `RequestSelectItem` 事件 |
| `LRS/Views/TopView.xaml.cs` | `OnNewFolderClick` / `OnNewFilePresetClick` 改为 `async void` 调 VM 公开方法 |
| `LRS/Views/MiddleFilesView.xaml.cs` | 订阅 `RequestSelectItem` → 设 `FileListView.SelectedItem` + `ScrollIntoView`；`OnNavigatedFrom` 取消订阅 |

---

## 假设与决策
- 重名检测用 `OrdinalIgnoreCase`（Windows 文件系统语义）。
- 「创建」按钮按 primary：校验失败时 `args.Cancel = true` 阻止关闭并显示 InfoBar 错误。
- 选中后**不**自动进入重命名（用户已选「先弹输入框」交互），如需改可用 F2。
- 不修改 `GenerateUniquePath`（旧调用方保留兼容）。
- 不修改 `FileSystemNodeViewModel`；`IsSelected` 字段对 grid 视觉生效由 `FileListView.SelectedItem` 触发。

---

## 验证步骤
1. `dotnet build` 预期通过（不引入新警告）。
2. 启动应用：
   - 点「新建文件夹」→ 弹对话框默认「新建文件夹」全选 → 输入「项目文档」→ 创建 → 列表出现「项目文档」且被选中。
   - 再点「新建文件夹」→ 输入「项目文档」→ 创建 → 错误 InfoBar「同名已存在」，对话框不关闭。
   - 点「新建文件 → Markdown 文档」→ 默认「新建Markdown文档」全选 → 改名为「README」→ 创建 → 出现「README.md」且被选中。
   - 点「新建文件 → 输入扩展名...」→ 输入「.log」→ 下一步 → 默认「新建文件」→ 改名为「app」→ 创建 → 出现「app.log」且被选中。
   - 创建后立刻使用 F2 改名，验证选中状态正确。
3. 切换目录后再新建，验证 `RefreshAndSelectAsync` 在新目录中找得到新项。
