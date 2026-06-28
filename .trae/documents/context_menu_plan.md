# 右键菜单完善计划（保持现有 WinUI 风格 + 注册表动态菜单）

## 一、现状分析

### 1.1 项目当前状态
- 项目是一个 WinUI 3 文件资源管理器（LRS - LiveRootStorage）
- 使用 MVVM 架构 + CommunityToolkit.Mvvm
- 设计风格：WinUI Fluent 风格，使用 ThemedIcon 图标 + CommandBarFlyout 右键菜单
- 已有基础文件操作功能：复制、剪切、粘贴、删除、重命名、新建文件夹/文档等
- 已有 ShellIconHelper 使用 Win32 API 获取系统图标，可作为参考

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
2. **右键菜单项是静态的** - 没有从 Windows 注册表读取动态的文件关联右键菜单项
3. **缺少注册表集成** - 无法根据文件类型显示系统中注册的第三方应用菜单项

## 二、实现目标

### 2.1 视觉风格
- **保持现有风格不变**：继续使用 `CommandBarFlyout` + `ThemedIcon` 的 WinUI Fluent 风格
- 注册表读取的菜单项以次要命令形式显示在下拉列表中
- 保持与项目现有 UI 一致的设计语言

### 2.2 功能目标

#### 2.2.1 注册表右键菜单读取
- 读取 Windows 注册表中文件关联的右键菜单项
- 支持以下注册表位置：
  - `HKEY_CLASSES_ROOT\*\shell` - 所有文件通用
  - `HKEY_CLASSES_ROOT\AllFilesystemObjects\shell` - 所有文件系统对象
  - `HKEY_CLASSES_ROOT\<扩展名>\shell` - 特定文件类型（如 .txt, .docx）
  - `HKEY_CLASSES_ROOT\Directory\shell` - 文件夹
  - `HKEY_CLASSES_ROOT\Directory\background\shell` - 文件夹背景
  - `HKEY_CLASSES_ROOT\Folder\shell` - 所有文件夹类型
  - `HKEY_CLASSES_ROOT\SystemFileAssociations\<扩展名>\shell` - 系统文件关联
- 解析菜单项名称、图标、命令行
- 缓存已读取的菜单，提升性能

#### 2.2.2 文件列表（MiddleFilesView）右键菜单
**文件/文件夹项右键菜单**：
- 主要命令：剪切、复制、粘贴、重命名、删除（保持现有）
- 次要命令：
  - 打开
  - 打开方式
  - --- 分隔线 ---
  - [注册表读取的动态菜单项]
  - --- 分隔线 ---
  - 剪切 / 复制 / 粘贴
  - --- 分隔线 ---
  - 重命名
  - 删除
  - --- 分隔线 ---
  - 复制路径
  - 属性

**空白区域右键菜单**：
- 主要命令：新建文件夹、新建文本文档（保持现有）
- 次要命令：
  - [注册表读取的背景菜单]
  - --- 分隔线 ---
  - 粘贴
  - --- 分隔线 ---
  - 刷新
  - 属性

#### 2.2.3 文件树（FileTreeView）右键菜单
**文件夹节点右键菜单**：
- 主要命令：展开/折叠、剪切、复制、粘贴、重命名
- 次要命令：
  - 打开
  - --- 分隔线 ---
  - [注册表读取的文件夹动态菜单项]
  - --- 分隔线 ---
  - 剪切 / 复制 / 粘贴
  - --- 分隔线 ---
  - 重命名
  - 删除
  - --- 分隔线 ---
  - 复制路径

## 三、技术方案

### 3.1 核心架构

#### 3.1.1 注册表菜单服务
新建 `ShellContextMenuService` 服务类，负责：
- 从注册表读取 shell 菜单项
- 根据文件路径/扩展名筛选对应菜单
- 解析命令字符串中的 `%1`, `%L` 等占位符
- 提取菜单图标（从注册表中指定的 DLL/EXE 提取）
- 缓存机制，避免重复读取

#### 3.1.2 菜单模型
新建 `ShellMenuItem` 模型类，包含：
- `Name` - 菜单显示名称
- `Command` - 命令行字符串
- `IconPath` - 图标路径（可选）
- `IconIndex` - 图标索引（可选）
- `SubItems` - 子菜单（可选）
- `IsSeparator` - 是否为分隔线
- `IsExtended` - 是否按住 Shift 才显示
- `MUIVerb` - MUI 资源字符串

### 3.2 注册表读取策略

#### 3.2.1 读取顺序（优先级从低到高）
1. `HKEY_CLASSES_ROOT\*\shell`（所有文件）
2. `HKEY_CLASSES_ROOT\AllFilesystemObjects\shell`
3. `HKEY_CLASSES_ROOT\SystemFileAssociations\<扩展名>\shell`
4. `HKEY_CLASSES_ROOT\<ProgID>\shell`（通过扩展名查找 ProgID）
5. 后读取的菜单项追加到前面（优先级高）

#### 3.2.2 文件夹菜单读取顺序
1. `HKEY_CLASSES_ROOT\Folder\shell`
2. `HKEY_CLASSES_ROOT\Directory\shell`
3. `HKEY_CLASSES_ROOT\AllFilesystemObjects\shell`

#### 3.2.3 文件夹背景菜单
1. `HKEY_CLASSES_ROOT\Directory\background\shell`
2. `HKEY_CLASSES_ROOT\AllFilesystemObjects\background\shell`

### 3.3 命令执行
- 使用 `Process.Start` 执行注册表中的命令行
- 替换占位符：`%1` / `%L` → 文件完整路径
- 支持 `%V` → 文件夹路径
- 支持 `rundll32.exe` 格式的命令

### 3.4 图标方案
- 主要命令：使用项目已有的 ThemedIcon 样式
- 注册表菜单项：从 DLL/EXE 中提取图标（复用 ShellIconHelper 的图标提取逻辑）
- 无图标的菜单项：使用默认图标或不显示图标

## 四、文件修改清单

| 文件 | 修改类型 | 说明 |
|------|----------|------|
| [ShellContextMenuService.cs](file:///workspace/LRS/Services/ShellContextMenuService.cs) | 新建 | 注册表右键菜单读取服务 |
| [ShellMenuItem.cs](file:///workspace/LRS/Models/ShellMenuItem.cs) | 新建 | 菜单项数据模型 |
| [MiddleFilesView.xaml.cs](file:///workspace/LRS/Views/MiddleFilesView.xaml.cs) | 修改 | 集成注册表动态菜单，优化菜单项结构 |
| [FileTreeView.xaml](file:///workspace/LRS/Views/FileTreeView.xaml) | 修改 | 为 TreeViewItem 添加右键菜单支持 |
| [FileTreeView.xaml.cs](file:///workspace/LRS/Views/FileTreeView.xaml.cs) | 修改 | 构建右键菜单，处理菜单项点击 |
| [MainWindowViewModel.cs](file:///workspace/LRS/ViewModels/MainWindowViewModel.cs) | 修改 | 新增 RefreshCommand，集成 ShellContextMenuService |
| [App.xaml.cs](file:///workspace/LRS/App.xaml.cs) | 修改 | 注册 ShellContextMenuService 到 DI 容器 |

## 五、详细实现步骤

### 步骤 1：创建菜单数据模型
- 新建 `ShellMenuItem` 类，包含名称、命令、图标、子菜单等属性

### 步骤 2：实现注册表菜单服务
- 新建 `ShellContextMenuService` 类
- 实现注册表路径枚举和读取逻辑
- 实现 ProgID 查找（通过扩展名找到对应的程序标识符）
- 实现菜单项解析（读取默认值、Command 子键、Icon 值等）
- 实现命令占位符替换
- 实现缓存机制（按扩展名缓存）

### 步骤 3：MainWindowViewModel 集成
- 注入 `ShellContextMenuService`
- 新增 `RefreshCommand`（RelayCommand）
- 新增获取动态菜单的方法或属性

### 步骤 4：优化 MiddleFilesView 右键菜单
- 保持 `CommandBarFlyout` 风格不变
- 动态加载注册表菜单项到次要命令列表
- 完善菜单项分组，增加分隔线
- 新增刷新、属性等菜单项
- 实现注册表菜单项的点击执行

### 步骤 5：FileTreeView 添加右键菜单
- 构建 `CommandBarFlyout` 菜单
- 动态加载文件夹类型的注册表菜单项
- 处理右键选中同步问题
- 实现菜单项点击事件

### 步骤 6：注册服务到 DI 容器
- 在 `App.xaml.cs` 中注册 `ShellContextMenuService` 为单例

### 步骤 7：编译测试
- 运行 `dotnet build` 确保编译通过
- 测试不同文件类型的右键菜单是否正确显示注册表项
- 测试菜单项点击是否正确执行命令
- 测试文件夹和空白区域右键菜单

## 六、潜在风险与处理

| 风险 | 影响 | 处理方案 |
|------|------|----------|
| 注册表读取权限不足 | 无法读取某些键值 | 捕获异常，静默跳过，只读取可访问的项 |
| 注册表菜单项命令格式复杂 | 无法正确执行 | 解析常见格式（exe直接调用、rundll32、cmd /c等），复杂的跳过 |
| 菜单项图标提取失败 | 菜单无图标 | 提供默认图标或无图标降级显示 |
| 大量注册表菜单项导致菜单过长 | 用户体验差 | 限制显示数量，或提供"更多"子菜单 |
| TreeViewItem 右键选中不同步 | 操作对象错误 | 在 RightTapped 事件中手动设置 SelectedItem |
| 性能问题（每次右键都读注册表） | 菜单显示延迟 | 实现缓存机制，按扩展名缓存菜单结构 |
| 打包模式（Msix）下注册表访问限制 | 无法读取注册表 | 使用 Win32 API 或降级为静态菜单 |
| 某些菜单项需要管理员权限 | 执行失败 | 捕获异常，提示用户或静默失败 |

## 七、验证标准

1. **编译通过**：`dotnet build` 无错误
2. **文件列表右键**：文件项和空白区域右键菜单正常显示
3. **文件树右键**：文件夹节点右键显示菜单，功能正常
4. **动态菜单**：不同文件类型显示对应的注册表右键菜单项
5. **命令执行**：注册表菜单项点击后能正确执行对应程序
6. **风格一致**：右键菜单风格与项目现有 UI 风格保持一致
7. **性能**：右键菜单显示无明显延迟（缓存生效）
