using LRS.Models;
using LRS.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LRS.Views
{
    public sealed partial class SettingsView : Page
    {
        public SettingsViewModel SettingsViewModel = new();
        public SettingsView()
        {
            DataContext = new SettingsViewModel();
            InitializeComponent();
            OrderModeComboBox.DataContext = SettingsViewModel;
            BuildExtensionsSection();
            if (App.SharedViewModel?.Extensions != null)
            {
                App.SharedViewModel.Extensions.Changed += OnExtensionsChanged;
            }
            this.Unloaded += (_, _) =>
            {
                if (App.SharedViewModel?.Extensions != null)
                {
                    App.SharedViewModel.Extensions.Changed -= OnExtensionsChanged;
                }
            };
        }

        private void OnExtensionsChanged(object? sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(BuildExtensionsSection);
        }

        private void SaveSettings(object sender, RoutedEventArgs e)
        {
            App.SharedViewModel.AppConfigs.SaveConfig();
        }
        public void OnSettingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        // === HLDS 扩展设置段 ===

        private void BuildExtensionsSection()
        {
            if (ExtensionsContainer == null || ExtensionSettingsContainer == null) return;
            ExtensionsContainer.Children.Clear();
            ExtensionSettingsContainer.Children.Clear();

            var mgr = App.SharedViewModel?.Extensions;
            var cfg = App.SharedViewModel?.AppConfigs;
            if (mgr == null || cfg == null) return;

            if (ExtensionDirectoryText != null)
                ExtensionDirectoryText.Text = mgr.ExtensionDirectory;

            if (mgr.LoadedExtensions.Count == 0)
            {
                ExtensionsContainer.Children.Add(new TextBlock
                {
                    Text = "（未找到扩展；请将 .hlds 放入扫描目录后重新启动）",
                    Opacity = 0.7,
                });
                return;
            }

            var disabled = mgr.GetDisabledIds();

            foreach (var ext in mgr.LoadedExtensions)
            {
                var card = new Border
                {
                    BorderThickness = new Thickness(1),
                    BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10),
                };
                var sp = new StackPanel { Spacing = 4 };
                var header = new TextBlock
                {
                    Text = $"{ext.Name}  v{ext.Version}",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                };
                sp.Children.Add(header);
                if (!string.IsNullOrEmpty(ext.Author))
                    sp.Children.Add(new TextBlock { Text = $"作者：{ext.Author}", Opacity = 0.8, FontSize = 12 });
                if (!string.IsNullOrEmpty(ext.Description))
                    sp.Children.Add(new TextBlock { Text = ext.Description, TextWrapping = TextWrapping.Wrap, Opacity = 0.8, FontSize = 12 });
                sp.Children.Add(new TextBlock
                {
                    Text = $"id: {ext.Id}    源文件: {ext.SourcePath}",
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas, Cascadia Mono, monospace"),
                    FontSize = 11,
                    Opacity = 0.6,
                });

                var toggle = new ToggleSwitch
                {
                    Header = "启用",
                    IsOn = !disabled.Contains(ext.Id),
                };
                var capturedExtId = ext.Id;
                toggle.Toggled += (s, e) =>
                {
                    mgr.SetEnabled(capturedExtId, toggle.IsOn);
                    if (toggle.IsOn) cfg.ExtensionsDisabledIds.Remove(capturedExtId);
                    else if (!cfg.ExtensionsDisabledIds.Contains(capturedExtId)) cfg.ExtensionsDisabledIds.Add(capturedExtId);
                    cfg.SaveConfig();
                };
                sp.Children.Add(toggle);
                card.Child = sp;
                ExtensionsContainer.Children.Add(card);
            }

            // 扩展设置项
            var typed = mgr.GetTypedContributions<SettingContribution>()
                .GroupBy(p => p.ExtensionId)
                .ToList();
            if (typed.Count == 0)
            {
                ExtensionSettingsContainer.Children.Add(new TextBlock
                {
                    Text = "（无扩展声明设置项）",
                    Opacity = 0.7,
                });
                return;
            }
            foreach (var grp in typed)
            {
                var ext = mgr.LoadedExtensions.FirstOrDefault(e => e.Id == grp.Key);
                if (ext == null) continue;
                var header = new TextBlock
                {
                    Text = $"{ext.Name}  ({grp.Key})",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 6, 0, 0),
                };
                ExtensionSettingsContainer.Children.Add(header);
                foreach (var (_, setting) in grp)
                {
                    ExtensionSettingsContainer.Children.Add(BuildSettingControl(grp.Key, setting, cfg));
                }
            }
        }

        private FrameworkElement BuildSettingControl(string extId, SettingContribution s, Configs cfg)
        {
            var current = cfg.GetExtensionSetting(extId, s.Key, s.Default ?? string.Empty);
            switch ((s.Type ?? "text").ToLowerInvariant())
            {
                case "toggle":
                    {
                        var sw = new ToggleSwitch { Header = s.Label, IsOn = string.Equals(current, "true", StringComparison.OrdinalIgnoreCase) || current == "1" };
                        sw.Toggled += (_, _) =>
                        {
                            cfg.SetExtensionSetting(extId, s.Key, sw.IsOn ? "true" : "false");
                            cfg.SaveConfig();
                        };
                        return sw;
                    }
                case "number":
                    {
                        var nb = new Microsoft.UI.Xaml.Controls.NumberBox
                        {
                            Header = s.Label,
                            Value = double.TryParse(current, out var v) ? v : 0,
                            SpinButtonPlacementMode = Microsoft.UI.Xaml.Controls.NumberBoxSpinButtonPlacementMode.Inline,
                        };
                        nb.ValueChanged += (_, _) =>
                        {
                            cfg.SetExtensionSetting(extId, s.Key, nb.Value.ToString());
                            cfg.SaveConfig();
                        };
                        return nb;
                    }
                case "combo":
                    {
                        var cb = new ComboBox { Header = s.Label, HorizontalAlignment = HorizontalAlignment.Stretch };
                        foreach (var opt in s.Options) cb.Items.Add(opt);
                        if (!string.IsNullOrEmpty(current)) cb.SelectedItem = current;
                        cb.SelectionChanged += (_, _) =>
                        {
                            if (cb.SelectedItem is string sel)
                            {
                                cfg.SetExtensionSetting(extId, s.Key, sel);
                                cfg.SaveConfig();
                            }
                        };
                        return cb;
                    }
                case "text":
                default:
                    {
                        var tb = new TextBox { Header = s.Label, Text = current ?? string.Empty };
                        tb.TextChanged += (_, _) =>
                        {
                            cfg.SetExtensionSetting(extId, s.Key, tb.Text ?? string.Empty);
                            cfg.SaveConfig();
                        };
                        return tb;
                    }
            }
        }
    }
}
