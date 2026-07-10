using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace LRS.ViewModels
{
    public partial class Configs : ObservableObject
    {
        private static readonly string DefaultConfigPath =
            Path.Combine(AppContext.BaseDirectory, "Configs", "configs.json");

        private static readonly string UserConfigDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LRS");

        public static readonly string UserConfigPath =
            Path.Combine(UserConfigDir, "user_configs.json");

        //public string UserConfigPathToDisplay = "C:C:C:C:C";
        public IConfiguration configuration;
        [ObservableProperty] private int _middleFilesHeight = 40;
        [ObservableProperty] private bool _ifUsesWin32APIToGetIcon = true;
        [ObservableProperty] private bool _ifLimitIconLoadingConcurrency = false;
        [ObservableProperty] private int _iconParallelLoadingCount = 30;
        [ObservableProperty] private string _homePageFullPath = "C:\\";
        [ObservableProperty] private string _defaultOrderMode = "ModifiedDesc";

        // ---- 扩展系统持久化 ----
        /// <summary>被禁用的扩展 ID 集合。ExtensionManager 在 InitializeAsync 前读取。</summary>
        [ObservableProperty] private ObservableCollection<string> _extensionsDisabledIds = new();

        /// <summary>各扩展的自定义设置值。键格式：<c>&lt;extensionId&gt;.&lt;key&gt;</c>。</summary>
        private readonly Dictionary<string, string> _extensionSettings = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, string> ExtensionSettings => _extensionSettings;

        public string GetExtensionSetting(string extensionId, string key, string fallback = "")
        {
            if (string.IsNullOrEmpty(extensionId) || string.IsNullOrEmpty(key)) return fallback;
            return _extensionSettings.TryGetValue($"{extensionId}.{key}", out var v) ? v : fallback;
        }

        public void SetExtensionSetting(string extensionId, string key, string value)
        {
            if (string.IsNullOrEmpty(extensionId) || string.IsNullOrEmpty(key)) return;
            _extensionSettings[$"{extensionId}.{key}"] = value ?? string.Empty;
        }

        public Configs()
        {
            EnsureUserConfigExists();
            BuildConfiguration();
            ReadConfigs();
            Debug.WriteLine($"Configs initialized. MiddleFilesHeight: {MiddleFilesHeight}, UserConfig: {UserConfigPath}");
        }

        private void EnsureUserConfigExists()
        {
            if (!File.Exists(UserConfigPath))
            {
                Directory.CreateDirectory(UserConfigDir);
                File.WriteAllText(UserConfigPath, "{}");
            }
        }

        private void BuildConfiguration()
        {
            configuration = new ConfigurationBuilder()
                .AddJsonFile(DefaultConfigPath, optional: false, reloadOnChange: false)
                .AddJsonFile(UserConfigPath, optional: true, reloadOnChange: false)
                .Build();
        }

        public void ReadConfigs()
        {
            MiddleFilesHeight = configuration.GetValue("Appearance:MiddleFilesHeight", 40);
            IfUsesWin32APIToGetIcon = configuration.GetValue("Advanced:ifUsesWin32APIToGetIcon", true);
            HomePageFullPath = configuration.GetValue("General:HomePageFullPath", "C:\\")!;
            IconParallelLoadingCount = configuration.GetValue("Performance:IconParallelLoadingCount", 30);
            DefaultOrderMode = configuration.GetValue("General:DefaultOrderMode", "ModifiedDesc")!;
            if (IconParallelLoadingCount != 0) IfLimitIconLoadingConcurrency = true;

            // 扩展相关
            ExtensionsDisabledIds.Clear();
            foreach (var id in configuration.GetSection("Extensions:DisabledIds").Get<string[]>() ?? Array.Empty<string>())
            {
                if (!string.IsNullOrEmpty(id)) ExtensionsDisabledIds.Add(id);
            }
            _extensionSettings.Clear();
            foreach (var kv in configuration.GetSection("Extensions:Settings").Get<Dictionary<string, string>>()
                     ?? new Dictionary<string, string>())
            {
                if (!string.IsNullOrEmpty(kv.Key)) _extensionSettings[kv.Key] = kv.Value ?? string.Empty;
            }
        }

        public void SaveConfig()
        {
            var extensions = new Dictionary<string, object?>
            {
                ["DisabledIds"] = ExtensionsDisabledIds.ToArray(),
                ["Settings"] = new Dictionary<string, string>(_extensionSettings),
            };
            var root = new Dictionary<string, object?>
            {
                ["Appearance"] = new Dictionary<string, object?>
                {
                    ["MiddleFilesHeight"] = MiddleFilesHeight,
                },
                ["Advanced"] = new Dictionary<string, object?>
                {
                    ["ifUsesWin32APIToGetIcon"] = IfUsesWin32APIToGetIcon,
                },
                ["General"] = new Dictionary<string, object?>
                {
                    ["HomePageFullPath"] = HomePageFullPath,
                    ["DefaultOrderMode"] = DefaultOrderMode,
                },
                ["Performance"] = new Dictionary<string, object?>
                {
                    ["IconParallelLoadingCount"] = IconParallelLoadingCount,
                },
                ["Extensions"] = extensions,
            };
            var json = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(UserConfigPath, json);
            if (configuration != null)
            {
                ((IConfigurationRoot)configuration).Reload();
                ReadConfigs();
            }
        }
    }
}
