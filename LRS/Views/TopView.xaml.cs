//using BetterBreadcrumbBar.Control;
//using LRS.ViewModels;
//using Microsoft.UI.Dispatching;
//using Microsoft.UI.Xaml;
using LRS.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
//using Microsoft.UI.Xaml.Controls.Primitives;
//using Microsoft.UI.Xaml.Data;
//using Microsoft.UI.Xaml.Input;
//using Microsoft.UI.Xaml.Media;
//using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
//using System.IO;
//using System.Linq;
//using System.Runtime.InteropServices.WindowsRuntime;
//using Windows.Foundation;
//using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

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
			App.SharedViewModel.NewFolder();
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
