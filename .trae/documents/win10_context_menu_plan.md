# Win10 风格右键菜单实现计划

## 一、现状分析

### 1.1 项目当前状态
- 项目是一个 WinUI 3 文件资源管理器（LRS - LiveRootStorage）
- 使用 MVVM 架构 + CommunityToolkit.Mvvm
- 已有基础文件操作功能：复制、剪切、粘贴、删除、重命名、新建文件夹/文档等

### 1.2 现有右键菜单
当前在 [MiddleFilesView.xaml.cs](file:///workspace/LRS/Views/MiddleFilesView.xaml.cs) 中使用 `CommandBarFlyout` 实现右键菜单：
- **文件项右键菜单**：剪切、复制、粘贴、重命名、删除（主要命令）+ 打开、打开方式、复制路径（次要命令）
- **空白区域右键菜单**：新建文件夹、新建文本文档 + 粘贴
- **文件树（FileTreeView）**：尚无右键菜单

### 1.3 存在的问题
- 当前使用 `CommandBarFlyout`，是 WinUI 风格的横排工具栏样式，与 Win10 经典右键菜单风格不符
- Win10 风格应为竖排列表式菜单，每个菜单项含图标 + 文字
- 文件树视图缺少右键菜单支持

## 二、实现目标

### 2.1 视觉目标
实现 Windows 10 资源管理器风格的右键菜单：
- 竖排列表布局（MenuFlyout 而非 CommandBarFlyout）
- 每个菜单项左侧图标 + 右侧文字
- 分隔线分组
- 悬停高亮效果
- 紧凑的行高和边距

### 2.2 功能目标
1. **文件列表（MiddleFilesView）右键菜单**
   - 文件/文件夹项右键：打开、打开方式、剪切、复制、粘贴、重命名、删除、复制路径
   - 空白区域右键：新建文件夹、新建文本文档、粘贴、刷新、属性
   
2. **文件树（FileTreeView）右键菜单**
   - 文件夹项右键：展开/折叠、打开、剪切、复制、粘贴、重命名、删除、复制路径

## 三、技术方案

### 3.1 核心控件选择
使用 `MenuFlyout` 替代 `CommandBarFlyout`，通过自定义样式模拟 Win10 外观：
- `MenuFlyoutItem` - 普通菜单项
- `MenuFlyoutSubItem` - 子菜单（如"打开方式"）
- `MenuFlyoutSeparator` - 分隔线

### 3.2 自定义样式
在 App.xaml 或资源字典中定义 Win10 风格的 MenuFlyoutItem 样式：
- 调整菜单项高度、内边距
- 统一图标和文字间距
- 悬停/按下状态的背景色

### 3.3 图标方案
沿用项目现有的 `ThemedIcon` 控件 + Segoe MDL2 Assets 字体图标，保持一致性。

## 四、文件修改清单

| 文件 | 修改类型 | 说明 |
|------|----------|------|
| [MiddleFilesView.xaml.cs](file:///workspace/LRS/Views/MiddleFilesView.xaml.cs) | 重写 | 用 MenuFlyout 替换 CommandBarFlyout，重新构建右键菜单 |
| [FileTreeView.xaml](file:///workspace/LRS/Views/FileTreeView.xaml) | 修改 | 为 TreeViewItem 添加 ContextFlyout |
| [FileTreeView.xaml.cs](file:///workspace/LRS/Views/FileTreeView.xaml.cs) | 修改 | 添加右键菜单事件处理逻辑 |
| [TreeDataGrid.xaml.cs](file:///workspace/LRS/UserControls/TreeDataGrid.xaml.cs) | 调整 | 确保 ContextFlyout 属性正确传递到 ListViewItem |
| [App.xaml](file:///workspace/LRS/App.xaml) | 修改 | 添加 Win10 风格 MenuFlyout 资源样式 |

## 五、详细实现步骤

### 步骤 1：定义 Win10 风格 MenuFlyout 样式
- 在 `App.xaml` 中添加 `MenuFlyoutItem`、`MenuFlyoutSubItem`、`MenuFlyoutSeparator` 的自定义样式
- 调整 Padding、Height、FontSize 以匹配 Win10 风格
- 设置悬停背景色为 Win10 经典浅蓝色

### 步骤 2：重写 MiddleFilesView 的右键菜单
- 删除现有的 `CommandBarFlyout` 构建逻辑
- 重新实现 `BuildItemContextFlyout()` 返回 `MenuFlyout`
- 重新实现 `BuildBaseContextFlyout()` 返回 `MenuFlyout`
- 菜单项分组（使用 `MenuFlyoutSeparator`）：
  - 打开/打开方式
  - 剪切/复制/粘贴
  - 重命名/删除
  - 复制路径
- 更新 `FileGrid.ContextFlyout` 和 `FileGrid.BaseContextFlyout` 赋值

### 步骤 3：为 FileTreeView 添加右键菜单
- 在 `FileTreeView.xaml` 的 `TreeView.ItemTemplate` 中为 `TreeViewItem` 添加 `ContextFlyout`
- 在 `FileTreeView.xaml.cs` 中构建菜单并处理菜单项点击事件
- 菜单项：展开/折叠、打开、剪切、复制、粘贴、重命名、删除、复制路径

### 步骤 4：验证 TreeDataGrid 的 ContextFlyout 传递
- 检查 [TreeDataGrid.xaml.cs](file:///workspace/LRS/UserControls/TreeDataGrid.xaml.cs) 中 `OnContainerContentChanging` 方法
- 确保 `ContextFlyout` 正确应用到每个 `ListViewItem`

### 步骤 5：编译测试
- 运行 `dotnet build` 确保编译通过
- 验证右键菜单在文件列表和文件树中的显示效果

## 六、潜在风险与处理

| 风险 | 影响 | 处理方案 |
|------|------|----------|
| MenuFlyout 默认样式与 Win10 差距大 | 视觉效果不佳 | 通过自定义 Style/ControlTemplate 精细调整 |
| 文件树 TreeViewItem 右键选中不同步 | 操作对象错误 | 在 RightTapped 事件中手动设置 SelectedItem |
| 现有命令绑定方式需调整 | 功能失效 | 保持 Command 绑定模式，仅更换 Flyout 类型 |
| 打包模式（Msix）下 Shell 上下文菜单限制 | 无法使用系统原生菜单 | 使用自定义 MenuFlyout 模拟，不调用系统 COM 接口 |

## 七、验证标准

1. **编译通过**：`dotnet build` 无错误
2. **文件列表右键**：在文件/文件夹项上右键显示 Win10 风格菜单
3. **空白区域右键**：在文件列表空白处右键显示新建菜单
4. **文件树右键**：在文件树节点上右键显示对应菜单
5. **菜单项功能**：所有菜单项功能正常（打开、剪切、复制、粘贴、重命名、删除、复制路径等）
6. **视觉风格**：菜单外观接近 Win10 资源管理器右键菜单
