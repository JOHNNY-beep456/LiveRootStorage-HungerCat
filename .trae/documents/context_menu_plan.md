# 右键菜单完善计划（保持现有 WinUI 风格）

## 一、现状分析

### 1.1 项目当前状态
- 项目是一个 WinUI 3 文件资源管理器（LRS - LiveRootStorage）
- 使用 MVVM 架构 + CommunityToolkit.Mvvm
- 设计风格：WinUI Fluent 风格，使用 ThemedIcon 图标 + CommandBarFlyout 右键菜单
- 已有基础文件操作功能：复制、剪切、粘贴、删除、重命名、新建文件夹/文档等

### 1.2 现有右键菜单
当前在 [MiddleFilesView.xaml.cs](file:///workspace/LRS/Views/MiddleFilesView.xaml.cs) 中已有右键菜单实现：

**文件项右键菜单（CommandBarFlyout）**：
- 主要命令（横排图标按钮）：剪切、复制、粘贴、重命名、删除
- 次要命令（下拉列表）：打开、打开方式、复制路径

**空白区域右键菜单（CommandBarFlyout）**：
- 主要命令：新建文件夹、新建文本文档
- 次要命令：粘贴

### 1.3 存在的不足
1. **文件树视图缺少右键菜单** - [FileTreeView.xaml](file:///workspace/LRS/Views/FileTreeView.xaml) 中的 TreeView 节点没有右键菜单
2. **右键菜单功能可扩展** - 可增加刷新、属性等常用菜单项
3. **菜单项组织可优化** - 参考 Win10 资源管理器的菜单分组方式，但保持现有视觉风格

## 二、实现目标

### 2.1 视觉风格
- **保持现有风格不变**：继续使用 `CommandBarFlyout` + `ThemedIcon` 的 WinUI Fluent 风格
- 保持与项目现有 UI 一致的设计语言
- 图标使用项目已有的 ThemedIcon 资源

### 2.2 功能目标

#### 2.2.1 文件列表（MiddleFilesView）右键菜单优化
**文件/文件夹项右键菜单**：
- 主要命令：剪切、复制、粘贴、重命名、删除（保持现有）
- 次要命令：
  - 打开
  - 打开方式
  - --- 分隔线 ---
  - 剪切
  - 复制
  - 粘贴
  - --- 分隔线 ---
  - 重命名
  - 删除
  - --- 分隔线 ---
  - 复制路径
  - 属性

**空白区域右键菜单**：
- 主要命令：新建文件夹、新建文本文档（保持现有）
- 次要命令：
  - 粘贴
  - --- 分隔线 ---
  - 刷新
  - 属性

#### 2.2.2 文件树（FileTreeView）新增右键菜单
**文件夹节点右键菜单**：
- 主要命令：展开/折叠、剪切、复制、粘贴、重命名
- 次要命令：
  - 打开
  - --- 分隔线 ---
  - 剪切
  - 复制
  - 粘贴
  - --- 分隔线 ---
  - 重命名
  - 删除
  - --- 分隔线 ---
  - 复制路径

## 三、技术方案

### 3.1 控件选择
- 继续使用 `CommandBarFlyout` 作为右键菜单容器
- 主要命令使用 `AppBarButton` + `ThemedIcon` 图标
- 次要命令使用 `AppBarButton` + `FontIcon` 或 `ThemedIcon`
- 分组使用 `AppBarSeparator` 分隔线

### 3.2 图标方案
- 主要命令：使用项目已有的 ThemedIcon 样式（Icon.Cut, Icon.Copy, Icon.Paste, Icon.Rename, Icon.Delete, Icon.Folder, Icon.Document 等）
- 次要命令：使用 Segoe MDL2 Assets 字体图标

### 3.3 命令绑定
- 复用 [MainWindowViewModel.cs](file:///workspace/LRS/ViewModels/MainWindowViewModel.cs) 中已有的 RelayCommand：
  - CopyCommand, CutCommand, PasteCommand
  - DeleteCommand, RenameCommand
  - CopyPathCommand
  - NewFolderCommand, NewTextDocumentCommand
- 新增命令（如需要）：
  - RefreshCommand - 刷新当前文件夹
  - PropertiesCommand - 显示属性（可先占位）

## 四、文件修改清单

| 文件 | 修改类型 | 说明 |
|------|----------|------|
| [MiddleFilesView.xaml.cs](file:///workspace/LRS/Views/MiddleFilesView.xaml.cs) | 修改 | 优化菜单项结构，增加刷新、属性等菜单项，完善分组 |
| [FileTreeView.xaml](file:///workspace/LRS/Views/FileTreeView.xaml) | 修改 | 为 TreeViewItem 添加 ContextFlyout |
| [FileTreeView.xaml.cs](file:///workspace/LRS/Views/FileTreeView.xaml.cs) | 修改 | 构建右键菜单，处理菜单项点击事件 |
| [MainWindowViewModel.cs](file:///workspace/LRS/ViewModels/MainWindowViewModel.cs) | 修改 | 新增 RefreshCommand，可选新增 PropertiesCommand |

## 五、详细实现步骤

### 步骤 1：MainWindowViewModel 新增命令
- 添加 `RefreshCommand`（RelayCommand）：调用已有的 `RefreshCurrentFolder()` 方法
- 可选：添加 `PropertiesCommand`（暂留 TODO 或使用消息框占位）

### 步骤 2：优化 MiddleFilesView 右键菜单
- 保持 `CommandBarFlyout` 风格不变
- 完善次要命令的分组，增加分隔线
- 新增菜单项：刷新（空白区域菜单）、属性（文件项菜单和空白区域菜单）
- 确保所有菜单项功能正常绑定

### 步骤 3：FileTreeView 添加右键菜单
- 在 `FileTreeView.xaml.cs` 中构建 `CommandBarFlyout` 菜单
- 主要命令：展开/折叠、剪切、复制、粘贴、重命名
- 次要命令：打开、剪切、复制、粘贴、重命名、删除、复制路径
- 在 TreeView 的 `RightTapped` 事件中处理右键选中
- 将菜单附加到 TreeViewItem 或通过代码方式显示

### 步骤 4：编译测试
- 运行 `dotnet build` 确保编译通过
- 验证文件列表右键菜单功能
- 验证文件树右键菜单功能
- 确保所有菜单项图标和文字显示正确

## 六、潜在风险与处理

| 风险 | 影响 | 处理方案 |
|------|------|----------|
| TreeViewItem 右键时选中状态不同步 | 操作对象错误 | 在 RightTapped 事件中手动设置 IsSelected 和 SelectedItem |
| 新增命令缺少图标资源 | 菜单显示不完整 | 使用 FontIcon 作为备选，或新增 ThemedIcon 资源 |
| 文件树菜单与文件列表菜单重复代码 | 维护困难 | 可提取公共菜单构建方法（视情况而定，优先保证功能） |

## 七、验证标准

1. **编译通过**：`dotnet build` 无错误
2. **文件列表右键**：文件项和空白区域右键菜单正常显示，风格与原有一致
3. **文件树右键**：文件夹节点右键显示菜单，功能正常
4. **菜单项功能**：所有菜单项功能正常（打开、剪切、复制、粘贴、重命名、删除、复制路径、刷新等）
5. **风格一致**：右键菜单风格与项目现有 UI 风格保持一致
