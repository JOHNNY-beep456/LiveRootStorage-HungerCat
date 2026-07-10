using LRS.Services;
using LRS.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LRS
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;
        public static Window? MainWindow { get; private set; }
        private IHost _host;
		public static MainWindowViewModel SharedViewModel { get; private set; }
		public static IServiceProvider Services { get; private set; }
        private static IServiceProvider ConfigureServices()
        {
			var services = new ServiceCollection();

			services.AddSingleton<Configs>();
			services.AddSingleton<IIconProvider, WindowsIconProvider>();

			return services.BuildServiceProvider();
		}
		/// <summary>
		/// Initializes the singleton application object.  This is the first line of authored code
		/// executed, and as such is the logical equivalent of main() or WinMain().
		/// </summary>
		public App()
        {
            Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
            InitializeComponent();
            _host = Host.CreateDefaultBuilder().ConfigureServices((context, services) =>
            {
                services.AddSingleton(new Configs());
				services.AddSingleton<IIconProvider, WindowsIconProvider>();
				services.AddSingleton<IFileOperator, FileOperator>();
				services.AddSingleton<ShellContextMenuService>();
				// ExtensionManager 在 Services 中暴露；构造时不需要 DispatcherQueue，
				// OnLaunched 中会通过 SetDispatcher 注入并 InitializeAsync。
				services.AddSingleton<ExtensionManager>();
			}).Build();
            Services = _host.Services;
			this.UnhandledException += (s, e) =>
			{
				Debug.WriteLine($"未处理异常: {e.Exception}");
				e.Handled = true; // 防止立即崩溃，方便调试
			};
		}

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
			await _host.StartAsync();

			// 在 UI 线程上创建共享 ViewModel
			var dispatcher = DispatcherQueue.GetForCurrentThread();
			var configs = Services.GetRequiredService<Configs>();
			var fileOperator = Services.GetRequiredService<IFileOperator>();
			var iconProvider = Services.GetRequiredService<IIconProvider>();
			var shellContextMenu = Services.GetRequiredService<ShellContextMenuService>();
			SharedViewModel = new MainWindowViewModel(iconProvider, dispatcher, configs, fileOperator, shellContextMenu);

			// 初始化扩展系统：注入 DispatcherQueue（用于把 Changed 事件 marshal 回 UI 线程），
			// 把 Configs 中持久化的禁用集合灌入，再扫描 ./ext/ 目录。
			var extensions = Services.GetRequiredService<ExtensionManager>();
			extensions.SetDispatcher(dispatcher);
			extensions.LoadDisabledIds(configs.ExtensionsDisabledIds);
			try
			{
				using var initCts = new System.Threading.CancellationTokenSource(
					TimeSpan.FromMilliseconds(1500));
				await extensions.InitializeAsync(initCts.Token);
			}
			catch (OperationCanceledException)
			{
				Debug.WriteLine("[App] ExtensionManager.InitializeAsync timed out (>1.5s).");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[App] ExtensionManager.InitializeAsync failed: {ex.Message}");
			}
			// 订阅热重载，刷新各 UI
			extensions.Changed += (_, _) => SharedViewModel.OnExtensionsChanged();

			_window = new Views.MainWindowView();
			MainWindow = _window;
			_window.Activate();
		}
    }
}
