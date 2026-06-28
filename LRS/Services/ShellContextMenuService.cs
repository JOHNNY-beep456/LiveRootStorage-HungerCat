using LRS.Models;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LRS.Services
{
    public class ShellContextMenuService
    {
        private readonly ConcurrentDictionary<string, List<ShellMenuItem>> _menuCache = new();

        public List<ShellMenuItem> GetFileContextMenu(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return new List<ShellMenuItem>();

            string extension = Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;
            string cacheKey = $"file_{extension}";

            if (_menuCache.TryGetValue(cacheKey, out var cached))
                return CloneMenuItems(cached);

            var items = new List<ShellMenuItem>();

            try
            {
                items.AddRange(GetMenuFromRegistry(@"*\shell"));
            }
            catch { }

            try
            {
                items.AddRange(GetMenuFromRegistry(@"AllFilesystemObjects\shell"));
            }
            catch { }

            try
            {
                if (!string.IsNullOrEmpty(extension))
                {
                    items.AddRange(GetMenuFromRegistry($@"SystemFileAssociations\{extension}\shell"));
                }
            }
            catch { }

            try
            {
                string? progId = GetProgIdFromExtension(extension);
                if (!string.IsNullOrEmpty(progId))
                {
                    items.AddRange(GetMenuFromRegistry($@"{progId}\shell"));
                }
            }
            catch { }

            items = FilterValidItems(items);
            _menuCache.TryAdd(cacheKey, CloneMenuItems(items));
            return items;
        }

        public List<ShellMenuItem> GetDirectoryContextMenu(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return new List<ShellMenuItem>();

            string cacheKey = "directory_menu";

            if (_menuCache.TryGetValue(cacheKey, out var cached))
                return CloneMenuItems(cached);

            var items = new List<ShellMenuItem>();

            try
            {
                items.AddRange(GetMenuFromRegistry(@"Folder\shell"));
            }
            catch { }

            try
            {
                items.AddRange(GetMenuFromRegistry(@"Directory\shell"));
            }
            catch { }

            try
            {
                items.AddRange(GetMenuFromRegistry(@"AllFilesystemObjects\shell"));
            }
            catch { }

            items = FilterValidItems(items);
            _menuCache.TryAdd(cacheKey, CloneMenuItems(items));
            return items;
        }

        public List<ShellMenuItem> GetDirectoryBackgroundMenu(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return new List<ShellMenuItem>();

            string cacheKey = "directory_bg_menu";

            if (_menuCache.TryGetValue(cacheKey, out var cached))
                return CloneMenuItems(cached);

            var items = new List<ShellMenuItem>();

            try
            {
                items.AddRange(GetMenuFromRegistry(@"Directory\Background\shell"));
            }
            catch { }

            try
            {
                items.AddRange(GetMenuFromRegistry(@"AllFilesystemObjects\Background\shell"));
            }
            catch { }

            items = FilterValidItems(items);
            _menuCache.TryAdd(cacheKey, CloneMenuItems(items));
            return items;
        }

        private List<ShellMenuItem> GetMenuFromRegistry(string relativePath)
        {
            var items = new List<ShellMenuItem>();

            try
            {
                using var key = Registry.ClassesRoot.OpenSubKey(relativePath);
                if (key == null)
                    return items;

                foreach (string subKeyName in key.GetSubKeyNames())
                {
                    if (subKeyName.Equals("shellex", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        if (subKey == null)
                            continue;

                        var item = ParseShellKey(subKey, subKeyName);
                        if (item != null)
                            items.Add(item);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return items;
        }

        private ShellMenuItem? ParseShellKey(RegistryKey key, string defaultName)
        {
            var item = new ShellMenuItem();

            string? defaultVal = key.GetValue(null) as string;
            item.Name = !string.IsNullOrEmpty(defaultVal) ? defaultVal : defaultName;

            string? extended = key.GetValue("Extended") as string;
            if (extended != null)
                item.IsExtended = true;

            string? muiVerb = key.GetValue("MUIVerb") as string;
            if (!string.IsNullOrEmpty(muiVerb))
            {
                item.MUIVerb = muiVerb;
                item.Name = ResolveMUIVerb(muiVerb) ?? item.Name;
            }

            string? iconVal = key.GetValue("Icon") as string;
            if (!string.IsNullOrEmpty(iconVal))
            {
                ParseIconValue(iconVal, out string iconPath, out int iconIndex);
                item.IconPath = iconPath;
                item.IconIndex = iconIndex;
            }

            using var commandKey = key.OpenSubKey("command");
            if (commandKey != null)
            {
                string? cmd = commandKey.GetValue(null) as string;
                if (!string.IsNullOrEmpty(cmd))
                {
                    item.Command = cmd;
                }
            }

            if (string.IsNullOrEmpty(item.Name))
                return null;

            if (item.Name.StartsWith("@", StringComparison.Ordinal))
                return null;

            return item;
        }

        private static string? ResolveMUIVerb(string muiVerb)
        {
            if (string.IsNullOrEmpty(muiVerb) || !muiVerb.StartsWith("@", StringComparison.Ordinal))
                return null;

            try
            {
                int commaIdx = muiVerb.IndexOf(',');
                if (commaIdx > 0)
                {
                    string dllPath = muiVerb.Substring(1, commaIdx - 1);
                    string idStr = muiVerb.Substring(commaIdx + 1);
                    if (int.TryParse(idStr, out int resourceId))
                    {
                        dllPath = Environment.ExpandEnvironmentVariables(dllPath);
                        return muiVerb;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static void ParseIconValue(string iconVal, out string iconPath, out int iconIndex)
        {
            iconPath = iconVal;
            iconIndex = 0;

            int commaIdx = iconVal.LastIndexOf(',');
            if (commaIdx > 0 && commaIdx < iconVal.Length - 1)
            {
                string afterComma = iconVal.Substring(commaIdx + 1).Trim();
                if (int.TryParse(afterComma, out int idx))
                {
                    iconPath = iconVal.Substring(0, commaIdx);
                    iconIndex = idx;
                }
            }

            iconPath = Environment.ExpandEnvironmentVariables(iconPath.Trim().Trim('"'));
        }

        private static string? GetProgIdFromExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return null;

            try
            {
                using var extKey = Registry.ClassesRoot.OpenSubKey(extension);
                if (extKey == null)
                    return null;

                string? progId = extKey.GetValue(null) as string;
                return progId;
            }
            catch
            {
                return null;
            }
        }

        private static List<ShellMenuItem> FilterValidItems(List<ShellMenuItem> items)
        {
            var result = new List<ShellMenuItem>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Name))
                    continue;

                if (item.IsSeparator)
                {
                    result.Add(item);
                    continue;
                }

                string key = item.Name.Trim();
                if (seenNames.Contains(key))
                    continue;

                seenNames.Add(key);
                result.Add(item);
            }

            return result;
        }

        private static List<ShellMenuItem> CloneMenuItems(List<ShellMenuItem> items)
        {
            var result = new List<ShellMenuItem>();
            foreach (var item in items)
            {
                result.Add(new ShellMenuItem
                {
                    Name = item.Name,
                    Command = item.Command,
                    IconPath = item.IconPath,
                    IconIndex = item.IconIndex,
                    IsSeparator = item.IsSeparator,
                    IsExtended = item.IsExtended,
                    MUIVerb = item.MUIVerb,
                    SubItems = new List<ShellMenuItem>(item.SubItems)
                });
            }
            return result;
        }

        public void ExecuteMenuItem(ShellMenuItem menuItem, string filePath)
        {
            if (menuItem == null || string.IsNullOrEmpty(menuItem.Command))
                return;

            try
            {
                string command = ReplaceCommandPlaceholders(menuItem.Command, filePath);
                var (fileName, arguments) = ParseCommandLine(command);

                if (string.IsNullOrEmpty(fileName))
                    return;

                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true
                };

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ShellContextMenu] Execute failed: {ex.Message}");
            }
        }

        private static string ReplaceCommandPlaceholders(string command, string filePath)
        {
            string result = command;

            result = result.Replace("%1", $"\"{filePath}\"", StringComparison.OrdinalIgnoreCase);
            result = result.Replace("%L", $"\"{filePath}\"", StringComparison.OrdinalIgnoreCase);
            result = result.Replace("%V", $"\"{filePath}\"", StringComparison.OrdinalIgnoreCase);
            result = result.Replace("%l", $"\"{filePath}\"", StringComparison.Ordinal);

            return result;
        }

        private static (string FileName, string Arguments) ParseCommandLine(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                return (string.Empty, string.Empty);

            commandLine = commandLine.Trim();

            if (commandLine.StartsWith("\"", StringComparison.Ordinal))
            {
                int endQuote = commandLine.IndexOf('"', 1);
                if (endQuote > 0)
                {
                    string fileName = commandLine.Substring(1, endQuote - 1);
                    string args = endQuote < commandLine.Length - 1
                        ? commandLine.Substring(endQuote + 1).Trim()
                        : string.Empty;
                    return (fileName, args);
                }
            }

            int firstSpace = commandLine.IndexOf(' ');
            if (firstSpace > 0)
            {
                string fileName = commandLine.Substring(0, firstSpace);
                string args = commandLine.Substring(firstSpace + 1).Trim();
                return (fileName, args);
            }

            return (commandLine, string.Empty);
        }

        public void ClearCache()
        {
            _menuCache.Clear();
        }
    }
}
