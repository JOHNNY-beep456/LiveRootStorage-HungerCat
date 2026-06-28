using LRS.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;

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

		private void OnNewFolderClick(object sender, RoutedEventArgs e)
		{
			App.SharedViewModel.NewFolderCommand.Execute(null);
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
