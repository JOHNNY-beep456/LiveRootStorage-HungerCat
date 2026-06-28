using LRS.Models;
using LRS.Services;
using LRS.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace LRS.Views
{
    public sealed partial class FileTreeView : Page
    {
        private MainWindowViewModel VM => App.SharedViewModel;
        private readonly CommandBarFlyout _treeItemContextFlyout;
        private FileSystemNodeViewModel? _rightTappedItem;

        public FileTreeView()
        {
            Configs configs = App.SharedViewModel.AppConfigs;
            try
            {
                InitializeComponent();
                var uiDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                IIconProvider iconProvider = new WindowsIconProvider();
                this.DataContext = VM;
                _treeItemContextFlyout = BuildTreeItemContextFlyout();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitializeComponent failed: {ex}");
                throw;
            }
        }

        private CommandBarFlyout BuildTreeItemContextFlyout()
        {
            var flyout = new CommandBarFlyout { AlwaysExpanded = true };

            flyout.PrimaryCommands.Add(PlainBtn("展开", "\uE930", OnExpandClick));
            flyout.PrimaryCommands.Add(PlainBtn("剪切", "\uE8C6", OnCutClick));
            flyout.PrimaryCommands.Add(PlainBtn("复制", "\uE8C8", OnCopyClick));
            flyout.PrimaryCommands.Add(PlainBtn("粘贴", "\uE77F", OnPasteClick));
            flyout.PrimaryCommands.Add(PlainBtn("重命名", "\uE8AC", OnRenameClick));

            flyout.SecondaryCommands.Add(PlainBtn("打开", "\uE8E5", OnOpenClick));
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

            flyout.Opening += OnTreeFlyoutOpening;

            return flyout;
        }

        private void OnTreeFlyoutOpening(object sender, object e)
        {
            if (_rightTappedItem == null) return;

            var shellMenuSvc = VM.ShellContextMenu;
            if (shellMenuSvc == null) return;

            var shellItems = shellMenuSvc.GetDirectoryContextMenu(_rightTappedItem.FullPath);
            if (shellItems.Count == 0) return;

            var secondary = _treeItemContextFlyout.SecondaryCommands;

            for (int i = secondary.Count - 1; i >= 0; i--)
            {
                if (secondary[i] is AppBarButton btn && btn.Tag is string tagStr && tagStr == "shell_dynamic")
                {
                    secondary.RemoveAt(i);
                }
            }

            int firstSepIdx = -1;
            int secondSepIdx = -1;
            for (int i = 0; i < secondary.Count; i++)
            {
                if (secondary[i] is AppBarSeparator)
                {
                    if (firstSepIdx < 0)
                    {
                        firstSepIdx = i;
                    }
                    else
                    {
                        secondSepIdx = i;
                        break;
                    }
                }
            }

            int insertPos = secondSepIdx >= 0 ? secondSepIdx + 1 : firstSepIdx >= 0 ? firstSepIdx + 1 : 0;

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
                btn.Click += (s, args) => OnShellMenuItemClick(shellItem);
                secondary.Insert(insertPos, btn);
                insertPos++;
            }
        }

        private void TreeView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            _rightTappedItem = null;

            var element = e.OriginalSource as FrameworkElement;
            var item = element?.DataContext as FileSystemNodeViewModel;
            if (item == null) return;

            _rightTappedItem = item;

            if (!item.IsSelected)
            {
                item.IsSelected = true;
            }

            _treeItemContextFlyout.ShowAt(element, e.GetPosition(element));
        }

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
            if (_rightTappedItem == null) return;
            VM.OpenItem(_rightTappedItem);
        }

        private void OnExpandClick(object sender, RoutedEventArgs e)
        {
            if (_rightTappedItem == null) return;
            _rightTappedItem.IsExpanded = !_rightTappedItem.IsExpanded;
        }

        private void OnCutClick(object sender, RoutedEventArgs e)
            => VM.CutCommand.Execute(_rightTappedItem);

        private void OnCopyClick(object sender, RoutedEventArgs e)
            => VM.CopyCommand.Execute(_rightTappedItem);

        private void OnPasteClick(object sender, RoutedEventArgs e)
            => VM.PasteCommand.Execute(null);

        private void OnRenameClick(object sender, RoutedEventArgs e)
        {
            _treeItemContextFlyout.Hide();
            VM.RenameCommand.Execute(_rightTappedItem);
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
            => VM.DeleteCommand.Execute(_rightTappedItem);

        private void OnCopyPathClick(object sender, RoutedEventArgs e)
            => VM.CopyPathCommand.Execute(_rightTappedItem);

        private void OnShellMenuItemClick(ShellMenuItem menuItem)
        {
            if (_rightTappedItem == null) return;
            VM.ShellContextMenu.ExecuteMenuItem(menuItem, _rightTappedItem.FullPath);
        }

        private void ToggleSettings(object sender, RoutedEventArgs e)
        {
            VM.IsSettingsOpen = !VM.IsSettingsOpen;

            if (VM.IsSettingsOpen)
            {
                SettingsBtnIcon.Glyph = "\uE72B";
                SettingsBtnLabel.Text = "返回";
            }
            else
            {
                SettingsBtnIcon.Glyph = "\uE713";
                SettingsBtnLabel.Text = "设置";
            }
        }

        private async void TreeView_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
        {
            Debug.WriteLine($"[TreeView_SelectionChanged] Entered. AddedItems count: {args.AddedItems.Count}");
            var selectedItem = args.AddedItems.FirstOrDefault() as FileSystemNodeViewModel;
            if (selectedItem is null)
            {
                Debug.WriteLine("[TreeView_SelectionChanged] No FileSystemNodeViewModel selected.");
                return;
            }
            await Task.Delay(50);
            Debug.WriteLine($"[TreeView_SelectionChanged] Selected item: {selectedItem.Name}, Type: {selectedItem.NodeTypeName}");
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                if (selectedItem is FileSystemNodeViewModel folder)
                {
                    Debug.WriteLine($"[TreeView_SelectionChanged] Setting SelectedFolder to {folder.FullPath}");
                    var vm = DataContext as MainWindowViewModel;
                    if (vm != null) vm.SelectedFolder = folder;
                }
                else if (!selectedItem.IsDirectory)
                {
                    Debug.WriteLine("[TreeView_SelectionChanged] Selected item is not a folder. Setting SelectedFolder to null.");
                }
            });
        }
    }
}
