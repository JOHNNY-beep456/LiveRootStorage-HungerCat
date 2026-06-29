using LRS.Models;
using LRS.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LRS.Views
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class TopView : Page
	{
		public TopView()
		{
			InitializeComponent();
			DataContext = App.SharedViewModel;
			App.SharedViewModel.BreadcrumbRefreshRequested += OnBreadcrumbRefreshRequested;
			BuildNewFileFlyout();
			// 首次构建扩展按钮 + 订阅热重载
			RebuildExtensionButtons();
			if (App.SharedViewModel?.Extensions != null)
			{
				App.SharedViewModel.Extensions.Changed += OnExtensionsChangedHandler;
			}
		}

		private void OnExtensionsChangedHandler(object? sender, EventArgs e)
		{
			DispatcherQueue.TryEnqueue(RebuildExtensionButtons);
		}

		private void OnBreadcrumbRefreshRequested()
		{
			DispatcherQueue.TryEnqueue(() => BreadcrumbCtrl.RefreshSegments());
		}

		private void BuildNewFileFlyout()
		{
			NewFileFlyout.Items.Clear();
			foreach (var preset in MainWindowViewModel.NewFilePresets)
			{
				var item = new MenuFlyoutItem
				{
					Text = $"{preset.DisplayName}  ({preset.Extension})",
					Tag = preset,
				};
				item.Click += OnNewFilePresetClick;
				NewFileFlyout.Items.Add(item);
			}
			NewFileFlyout.Items.Add(new MenuFlyoutSeparator());
			var inputItem = new MenuFlyoutItem { Text = "输入扩展名...", Tag = "INPUT" };
			inputItem.Click += OnNewFilePresetClick;
			NewFileFlyout.Items.Add(inputItem);
		}

		/// <summary>
		/// 从 <see cref="MainWindowViewModel.Extensions"/> 拉取 <see cref="TopbarButtonContribution"/>，
		/// 按 <see cref="TopbarButtonContribution.Position"/> 升序、相同 <c>Position</c> 时按
		/// 扩展 <c>id</c> 字典序填充 <see cref="ExtensionButtonsHost"/>。
		/// </summary>
		private void RebuildExtensionButtons()
		{
			if (ExtensionButtonsHost == null) return;
			ExtensionButtonsHost.Items.Clear();

			var vm = App.SharedViewModel;
			var mgr = vm?.Extensions;
			if (mgr == null) return;

			var buttons = mgr.GetTypedContributions<TopbarButtonContribution>()
				.OrderBy(p => p.Contribution.Position)
				.ThenBy(p => p.ExtensionId, StringComparer.Ordinal)
				.ToList();

			foreach (var pair in buttons)
			{
				var extId = pair.ExtensionId;
				var btn = pair.Contribution;
				if (string.IsNullOrWhiteSpace(btn.Label) || string.IsNullOrWhiteSpace(btn.Command)) continue;
				var b = new Button
				{
					Style = (Style)Application.Current.Resources["SubtleButtonStyle"],
					Height = 32,
					Padding = new Thickness(8, 2, 8, 2),
					Tag = $"extension_dynamic:{extId}:{btn.Id}",
				};
				ToolTipService.SetToolTip(b, btn.Label);
				var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
				sp.Children.Add(new FontIcon { Glyph = "\uE8E5", FontSize = 14 });
				sp.Children.Add(new TextBlock { Text = btn.Label, FontSize = 13 });
				b.Content = sp;
				var capturedExtId = extId;
				var capturedCmdId = btn.Id;
				b.Click += async (s, e) => await OnExtensionTopbarClick(capturedExtId, capturedCmdId);
				ExtensionButtonsHost.Items.Add(b);
			}
		}

		private async System.Threading.Tasks.Task OnExtensionTopbarClick(string extId, string cmdId)
		{
			var vm = App.SharedViewModel;
			var mgr = vm?.Extensions;
			if (mgr == null) return;
			var currentDir = vm.SelectedFolder?.FullPath ?? vm.CurrentBreadcrumbPath;
			var ctx = new ExtensionContext(
				File: null,
				Directory: currentDir,
				List: null);
			try
			{
				var r = await mgr.ExecuteAsync(extId, cmdId, ctx);
				if (r.ExitCode != 0)
				{
					Debug.WriteLine($"[Extension topbar] '{extId}/{cmdId}' exit={r.ExitCode} err={r.Error}");
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[Extension topbar] click failed: {ex.Message}");
			}
		}

		private async void OnNewFolderClick(object sender, RoutedEventArgs e)
		{
			try { await App.SharedViewModel.NewFolder(); }
			catch (Exception ex) { Debug.WriteLine($"[TopView] NewFolder failed: {ex.Message}"); }
		}

		private void OnNewFileButtonClick(object sender, RoutedEventArgs e)
		{
			// Flyout 自身管理弹出，这里什么都不做
		}

		private async void OnNewFilePresetClick(object sender, RoutedEventArgs e)
		{
			if (sender is not MenuFlyoutItem mfi || mfi.Tag == null) return;
			try
			{
				if (mfi.Tag is string s && s == "INPUT")
				{
					await App.SharedViewModel.NewFileWithInputExtensionAsync();
				}
				else if (mfi.Tag is MainWindowViewModel.NewFilePreset preset)
				{
					await App.SharedViewModel.NewFileWithPresetAsync(preset);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[TopView] NewFile failed: {ex.Message}");
			}
		}
	}
}
