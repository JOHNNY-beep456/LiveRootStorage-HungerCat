using LRS.Services;
using LRS.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace LRS.Views
{
    public sealed partial class MainWindowView : Window
    {
        private MainWindowViewModel VM => App.SharedViewModel;

        public MainWindowView()
        {
            InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            VM.PropertyChanged += OnVMPropertyChanged;
            UpdateRightPanel();
        }

        private void OnVMPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsSettingsOpen))
                UpdateRightPanel();
        }

        private void UpdateRightPanel()
        {
            if (VM.IsSettingsOpen)
            {
                MiddleFilesPanel.Visibility = Visibility.Collapsed;
                SettingsPanelView.Visibility = Visibility.Visible;
            }
            else
            {
                MiddleFilesPanel.Visibility = Visibility.Visible;
                SettingsPanelView.Visibility = Visibility.Collapsed;
            }
        }

        // ---- 拖拽安装扩展 ----

        private void OnRootDragOver(object sender, DragEventArgs e)
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.None;
                return;
            }

            // 只在 DataView 里能拿到至少一个 .hlds 文件时才接受
            // （DataPackageView 的 StorageItems 列表是延迟获取的，这里用 Deferral
            //  也可以；为了简单，仅根据 Contains 判断并直接接受 Copy 即可，
            //  最终在 Drop 时再次过滤具体扩展名）
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "安装扩展";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
            e.DragUIOverride.IsContentVisible = true;
            DropOverlay.Visibility = Visibility.Visible;
            e.Handled = true;
        }

        private void OnRootDragLeave(object sender, DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
        }

        private async void OnRootDrop(object sender, DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

            var deferral = e.GetDeferral();
            try
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var hldsFiles = items
                    .OfType<StorageFile>()
                    .Where(f => string.Equals(f.FileType, ".hlds", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (hldsFiles.Count == 0)
                {
                    await ShowDialogAsync(
                        "未识别扩展文件",
                        "请拖入 .hlds 格式的扩展清单文件。当前拖入的对象中没有匹配项。",
                        "确定");
                    return;
                }

                var mgr = App.Services.GetService(typeof(ExtensionManager)) as ExtensionManager;
                if (mgr == null)
                {
                    await ShowDialogAsync("扩展管理器未就绪", "ExtensionManager 服务未注册，无法安装。", "确定");
                    return;
                }

                var ok = new List<ExtensionManager.InstallResult>();
                var failed = new List<ExtensionManager.InstallResult>();
                foreach (var f in hldsFiles)
                {
                    var result = await mgr.InstallAsync(f.Path);
                    if (result.Success) ok.Add(result);
                    else failed.Add(result);
                }

                await ShowInstallSummaryAsync(ok, failed);
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("安装失败", ex.Message, "确定");
            }
            finally
            {
                deferral.Complete();
            }
        }

        private async Task ShowInstallSummaryAsync(
            List<ExtensionManager.InstallResult> ok,
            List<ExtensionManager.InstallResult> failed)
        {
            var sb = new StringBuilder();

            if (ok.Count > 0)
            {
                sb.Append("已安装 ");
                sb.Append(ok.Count);
                sb.AppendLine(ok.Count == 1 ? " 个扩展：" : " 个扩展：");
                foreach (var r in ok)
                {
                    var label = !string.IsNullOrEmpty(r.InstalledName) ? r.InstalledName : r.FileName;
                    var id = !string.IsNullOrEmpty(r.InstalledId) ? $"（{r.InstalledId}）" : string.Empty;
                    sb.Append("  ✓ ");
                    sb.Append(label);
                    sb.Append(' ');
                    sb.Append(id);
                    sb.AppendLine();
                }
            }

            if (failed.Count > 0)
            {
                sb.Append("失败 ");
                sb.Append(failed.Count);
                sb.AppendLine(failed.Count == 1 ? " 个：" : " 个：");
                foreach (var r in failed)
                {
                    sb.Append("  ✗ ");
                    sb.Append(r.FileName);
                    sb.Append(" — ");
                    sb.AppendLine(r.Error ?? "未知错误");
                }
            }

            if (ok.Count == 0 && failed.Count == 0)
            {
                sb.AppendLine("未处理任何文件。");
            }

            var title = failed.Count == 0
                ? "扩展安装完成"
                : (ok.Count == 0 ? "扩展安装失败" : "扩展安装部分完成");

            await ShowDialogAsync(title, sb.ToString().TrimEnd(), "确定");
        }

        private async Task ShowDialogAsync(string title, string content, string closeButtonText)
        {
            var dlg = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = closeButtonText,
                XamlRoot = (Content as UIElement)?.XamlRoot,
                DefaultButton = ContentDialogButton.Close,
            };
            await dlg.ShowAsync();
        }
    }
}
