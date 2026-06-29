using LRS.Models;
using LRS.Services;
using LRS.UserControls;
using LRS.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LRS.Views
{
    public sealed partial class MiddleFilesView : Page
    {
        private readonly CommandBarFlyout _itemContextFlyout;
        private readonly CommandBarFlyout _baseContextFlyout;

        public MiddleFilesView()
        {
            InitializeComponent();
            this.DataContext = App.SharedViewModel;
            _itemContextFlyout = BuildItemContextFlyout();
            _baseContextFlyout = BuildBaseContextFlyout();
            FileGrid.ContextFlyout = _itemContextFlyout;
            FileGrid.BaseContextFlyout = _baseContextFlyout;
            FileGrid.ItemRightTapped += OnItemRightTapped;
            if (App.SharedViewModel != null)
            {
                App.SharedViewModel.RequestSelectItem += OnRequestSelectItem;
            }
            this.Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (App.SharedViewModel != null)
            {
                App.SharedViewModel.RequestSelectItem -= OnRequestSelectItem;
            }
        }

        private void OnRequestSelectItem(FileSystemNodeViewModel? item)
        {
            if (item == null) return;
            DispatcherQueue.TryEnqueue(() => FileGrid.SelectItem(item));
        }

        private void OnTreeDataGridItemInvoked(object sender, FileSystemNodeViewModel item)
        {
            (this.DataContext as MainWindowViewModel)?.OpenItem(item);
        }

        private void OnItemRightTapped(object? sender, FileSystemNodeViewModel item)
        {
            UpdateItemContextFlyout(item);
        }

        private CommandBarFlyout BuildItemContextFlyout()
        {
            var flyout = new CommandBarFlyout { AlwaysExpanded = true };

            flyout.PrimaryCommands.Add(ThemedBtn("剪切", ThemedIconKey("Icon.Cut"), OnCutClick));
            flyout.PrimaryCommands.Add(ThemedBtn("复制", ThemedIconKey("Icon.Copy"), OnCopyClick));
            flyout.PrimaryCommands.Add(ThemedBtn("粘贴", ThemedIconKey("Icon.Paste"), OnPasteClick));
            flyout.PrimaryCommands.Add(ThemedBtn("重命名", ThemedIconKey("Icon.Rename"), OnRenameClick));
            flyout.PrimaryCommands.Add(ThemedBtn("删除", ThemedIconKey("Icon.Delete"), OnDeleteClick));

            flyout.SecondaryCommands.Add(PlainBtn("打开", "\uE8E5", OnOpenClick));
            flyout.SecondaryCommands.Add(PlainBtn("打开方式", "\uE8E5", OnOpenClick));
            flyout.SecondaryCommands.Add(new AppBarSeparator());
            flyout.SecondaryCommands.Add(new AppBarSeparator());
            flyout.SecondaryCommands.Add(PlainBtn("剪切", "\uE8C6", OnCutClick));
            flyout.SecondaryCommands.Add(PlainBtn("复制", "\uE8C8", OnCopyClick));
            flyout.SecondaryCommands.Add(PlainBtn("粘贴", "\uE77F", OnPasteClick));
            flyout.SecondaryCommands.Add(new AppBarSeparator());
            flyout.SecondaryCommands.Add(PlainBtn("重命名", "\uE8AC", OnRenameClick));
            flyout.SecondaryCommands.Add(PlainBtn("删除", "\uE74D", OnDeleteClick));
            flyout.SecondaryCommands.Add(new AppBarSeparator());
            flyout.SecondaryCommands.Add(PlainBtn("复制路径", "\uE8C8", OnCopyPathClick));
            flyout.SecondaryCommands.Add(PlainBtn("属性", "\uE946", OnPropertiesClick));

            return flyout;
        }

        private CommandBarFlyout BuildBaseContextFlyout()
        {
            var flyout = new CommandBarFlyout { AlwaysExpanded = true };

            flyout.PrimaryCommands.Add(ThemedBtn("新建文件夹", ThemedIconKey("Icon.Folder"), OnNewFolderClick));
            flyout.PrimaryCommands.Add(ThemedBtn("新建文本文档", ThemedIconKey("Icon.Document"), OnNewTextDocumentClick));

            flyout.SecondaryCommands.Add(new AppBarSeparator());
            flyout.SecondaryCommands.Add(PlainBtn("粘贴", "\uE77F", OnPasteClick));
            flyout.SecondaryCommands.Add(new AppBarSeparator());
            flyout.SecondaryCommands.Add(PlainBtn("刷新", "\uE72C", OnRefreshClick));
            flyout.SecondaryCommands.Add(PlainBtn("属性", "\uE946", OnPropertiesClick));

            flyout.Opening += OnBaseFlyoutOpening;

            return flyout;
        }

        private void UpdateItemContextFlyout(FileSystemNodeViewModel item)
        {
            var vm = this.DataContext as MainWindowViewModel;
            if (vm == null) return;

            var shellMenuSvc = vm.ShellContextMenu;
            if (shellMenuSvc == null) return;

            List<ShellMenuItem> shellItems;
            if (item.IsDirectory)
            {
                shellItems = shellMenuSvc.GetDirectoryContextMenu(item.FullPath);
            }
            else
            {
                shellItems = shellMenuSvc.GetFileContextMenu(item.FullPath);
            }

            if (shellItems.Count == 0) return;

            var secondary = _itemContextFlyout.SecondaryCommands;

            int firstSeparatorIdx = -1;
            int secondSeparatorIdx = -1;
            for (int i = 0; i < secondary.Count; i++)
            {
                if (secondary[i] is AppBarSeparator)
                {
                    if (firstSeparatorIdx < 0)
                    {
                        firstSeparatorIdx = i;
                    }
                    else
                    {
                        secondSeparatorIdx = i;
                        break;
                    }
                }
            }

            if (firstSeparatorIdx < 0 || secondSeparatorIdx < 0) return;

            int insertPos = secondSeparatorIdx + 1;

            for (int i = secondary.Count - 1; i > firstSeparatorIdx; i--)
            {
                if (secondary[i] is AppBarButton btn && btn.Tag is string tagStr && tagStr == "shell_dynamic")
                {
                    secondary.RemoveAt(i);
                }
            }

            foreach (var shellItem in shellItems)
            {
                if (shellItem.IsSeparator) continue;
                if (shellItem.IsExtended) continue;
                if (string.IsNullOrEmpty(shellItem.Command)) continue;
                if (string.IsNullOrEmpty(shellItem.Name)) continue;

                var btn = new AppBarButton
                {
                    Label = shellItem.Name,
                    Icon = new FontIcon { Glyph = "\uE756", FontSize = 16 },
                    Tag = "shell_dynamic"
                };
                btn.Click += (s, e) => OnShellMenuItemClick(shellItem, item.FullPath);
                secondary.Insert(insertPos, btn);
                insertPos++;
            }

            // 在 Shell 动态项之后再追加扩展项
            AppendExtensionContextMenuItems(_itemContextFlyout, item, vm.CurrentBreadcrumbPath);
        }

        private void OnBaseFlyoutOpening(object? sender, object e)
        {
            var vm = this.DataContext as MainWindowViewModel;
            if (vm == null) return;

            var shellMenuSvc = vm.ShellContextMenu;
            if (shellMenuSvc == null) return;

            string folderPath = vm.CurrentBreadcrumbPath;
            var shellItems = shellMenuSvc.GetDirectoryBackgroundMenu(folderPath);

            if (shellItems.Count == 0) return;

            var secondary = _baseContextFlyout.SecondaryCommands;

            for (int i = secondary.Count - 1; i >= 0; i--)
            {
                if (secondary[i] is AppBarButton btn && btn.Tag is string tagStr && tagStr == "shell_dynamic")
                {
                    secondary.RemoveAt(i);
                }
            }

            int firstSepIdx = -1;
            for (int i = 0; i < secondary.Count; i++)
            {
                if (secondary[i] is AppBarSeparator)
                {
                    firstSepIdx = i;
                    break;
                }
            }

            int insertPos = firstSepIdx >= 0 ? firstSepIdx : 0;

            for (int i = shellItems.Count - 1; i >= 0; i--)
            {
                var shellItem = shellItems[i];
                if (shellItem.IsSeparator) continue;
                if (shellItem.IsExtended) continue;
                if (string.IsNullOrEmpty(shellItem.Command)) continue;
                if (string.IsNullOrEmpty(shellItem.Name)) continue;

                var btn = new AppBarButton
                {
                    Label = shellItem.Name,
                    Icon = new FontIcon { Glyph = "\uE756", FontSize = 16 },
                    Tag = "shell_dynamic"
                };
                btn.Click += (s, args) => OnShellMenuItemClick(shellItem, folderPath);
                secondary.Insert(insertPos, btn);
            }

            // 追加扩展项到背景菜单
            AppendExtensionContextMenuItems(_baseContextFlyout, target: null, currentDirectory: folderPath);
        }

        private void OnShellMenuItemClick(ShellMenuItem menuItem, string filePath)
        {
            var vm = this.DataContext as MainWindowViewModel;
            if (vm == null) return;

            vm.ShellContextMenu.ExecuteMenuItem(menuItem, filePath);
        }

        // === HLDS 扩展：右键菜单贡献 ===

        /// <summary>
        /// 把当前已加载扩展的 <c>points.context_menu</c> 项追加到指定 flyout。
        /// 同一 flyout 上旧的扩展项（Tag=<c>"extension_dynamic:*"</c>）先被清空。
        /// </summary>
        /// <param name="flyout">要注入的右键菜单。</param>
        /// <param name="target">本次右键的目标（用于 <see cref="ContextMenuApplyTo"/> 过滤）。</param>
        /// <param name="currentDirectory">当前目录（用于 <c>Background</c> 时的 <c>%D</c>）。</param>
        private void AppendExtensionContextMenuItems(
            CommandBarFlyout flyout,
            FileSystemNodeViewModel? target,
            string currentDirectory)
        {
            if (flyout == null) return;
            var vm = this.DataContext as MainWindowViewModel;
            if (vm?.Extensions == null) return;

            var secondary = flyout.SecondaryCommands;

            // 清掉旧的扩展项
            for (int i = secondary.Count - 1; i >= 0; i--)
            {
                if (secondary[i] is AppBarButton btn
                    && btn.Tag is string tagStr
                    && tagStr.StartsWith("extension_dynamic:", StringComparison.Ordinal))
                {
                    secondary.RemoveAt(i);
                }
            }

            var typed = vm.Extensions.GetTypedContributions<ContextMenuContribution>();
            if (typed.Count == 0) return;

            // 决定插入位置：尽量放到 flyout 末尾、Shell 项之前
            int insertPos = secondary.Count;
            for (int i = secondary.Count - 1; i >= 0; i--)
            {
                if (secondary[i] is AppBarSeparator)
                {
                    insertPos = i;
                    break;
                }
            }

            // 在插入位置前再放一个分隔符（让扩展项与内置项明显分组）
            secondary.Insert(insertPos, new AppBarSeparator());
            insertPos++;

            foreach (var (extId, item) in typed)
            {
                if (!ShouldApplyTo(item.ApplyTo, target)) continue;
                if (string.IsNullOrWhiteSpace(item.Label) || string.IsNullOrWhiteSpace(item.Command)) continue;

                var btn = new AppBarButton
                {
                    Label = item.Label,
                    Icon = new FontIcon { Glyph = "\uE8E5", FontSize = 16 },
                    Tag = $"extension_dynamic:{extId}:{item.Id}",
                };
                var capturedExtId = extId;
                var capturedCmdId = item.Id;
                var capturedItem = item;
                btn.Click += async (s, args) =>
                {
                    await OnExtensionMenuItemClick(capturedExtId, capturedCmdId, capturedItem, target, currentDirectory);
                };
                secondary.Insert(insertPos, btn);
                insertPos++;
            }
        }

        private static bool ShouldApplyTo(ContextMenuApplyTo applyTo, FileSystemNodeViewModel? target)
        {
            return applyTo switch
            {
                ContextMenuApplyTo.Any => true,
                ContextMenuApplyTo.File => target != null && !target.IsDirectory,
                ContextMenuApplyTo.Folder => target != null && target.IsDirectory,
                ContextMenuApplyTo.Background => target == null,
                _ => true,
            };
        }

        private async Task OnExtensionMenuItemClick(
            string extensionId,
            string commandId,
            ContextMenuContribution contribution,
            FileSystemNodeViewModel? target,
            string currentDirectory)
        {
            var vm = this.DataContext as MainWindowViewModel;
            if (vm?.Extensions == null) return;

            var paths = FileGrid.SelectedItems
                .OfType<FileSystemNodeViewModel>()
                .Select(n => n.FullPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();
            if (paths.Count == 0 && target != null) paths.Add(target.FullPath);

            var ctx = new ExtensionContext(
                File: target?.FullPath ?? (paths.Count == 1 ? paths[0] : null),
                Directory: currentDirectory,
                List: paths.Count > 0 ? paths : null);

            try
            {
                var result = await vm.Extensions.ExecuteAsync(extensionId, commandId, ctx);
                if (result.ExitCode != 0)
                {
                    Debug.WriteLine($"[Extension] '{extensionId}/{commandId}' exit={result.ExitCode} err={result.Error}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Extension] click handler failed: {ex.Message}");
            }
        }

        private AppBarButton ThemedBtn(string label, string styleKey, RoutedEventHandler? click)
        {
            var icon = new ThemedIcon();
            icon.Style = (Style)Application.Current.Resources[styleKey];
            var btn = new AppBarButton { Label = label, Content = icon };
            if (click != null) btn.Click += click;
            return btn;
        }

        private static string ThemedIconKey(string name) => name;

        private static AppBarButton PlainBtn(string label, string glyph, RoutedEventHandler? click)
        {
            var btn = new AppBarButton
            {
                Label = label,
                Icon = new FontIcon { Glyph = glyph, FontSize = 16 }
            };
            if (click != null) btn.Click += click;
            return btn;
        }

        // === Click handlers ===
        private void OnOpenClick(object sender, RoutedEventArgs e)
        {
            var item = FileGrid.SelectedItem;
            if (item == null) return;
            (this.DataContext as MainWindowViewModel)?.OpenItem(item);
        }
        private void OnCutClick(object sender, RoutedEventArgs e)
            => (this.DataContext as MainWindowViewModel)?.CutCommand.Execute(FileGrid.SelectedItem);
        private void OnCopyClick(object sender, RoutedEventArgs e)
            => (this.DataContext as MainWindowViewModel)?.CopyCommand.Execute(FileGrid.SelectedItem);
        private void OnPasteClick(object sender, RoutedEventArgs e)
            => (this.DataContext as MainWindowViewModel)?.PasteCommand.Execute(null);
        private void OnRenameClick(object sender, RoutedEventArgs e)
        {
            _itemContextFlyout.Hide();
            (this.DataContext as MainWindowViewModel)?.RenameCommand.Execute(FileGrid.SelectedItem);
        }
        private void OnDeleteClick(object sender, RoutedEventArgs e)
            => (this.DataContext as MainWindowViewModel)?.DeleteCommand.Execute(FileGrid.SelectedItem);
        private void OnCopyPathClick(object sender, RoutedEventArgs e)
            => (this.DataContext as MainWindowViewModel)?.CopyPathCommand.Execute(FileGrid.SelectedItem);
        private void OnNewFolderClick(object sender, RoutedEventArgs e)
            => (this.DataContext as MainWindowViewModel)?.NewFolderCommand.Execute(null);
        private void OnNewTextDocumentClick(object sender, RoutedEventArgs e)
            => (this.DataContext as MainWindowViewModel)?.NewTextDocumentCommand.Execute(null);
        private void OnRefreshClick(object sender, RoutedEventArgs e)
            => (this.DataContext as MainWindowViewModel)?.RefreshCommand.Execute(null);
        private void OnPropertiesClick(object sender, RoutedEventArgs e)
        {
            var item = FileGrid.SelectedItem;
            string path = item?.FullPath ?? (this.DataContext as MainWindowViewModel)?.CurrentBreadcrumbPath ?? "";
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Properties] Error: {ex.Message}");
            }
        }
    }
}
