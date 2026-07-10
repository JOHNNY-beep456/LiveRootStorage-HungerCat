using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI;
using LRS.Services;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LRS.ViewModels
{
    /// <summary>
    /// 单条搜索结果（文件 / 文件夹）。不直接使用 <see cref="FileSystemNodeViewModel"/>，
    /// 因为结果可能来自任意深度的目录，而 ListView/TreeDataGrid 不便展示非当前目录的节点。
    /// </summary>
    public sealed class SearchResultItem
    {
        public required string Name { get; init; }
        public required string FullPath { get; init; }
        public required string ParentPath { get; init; }
        public required bool IsDirectory { get; init; }
        public required long Size { get; init; }
        public required string VisualSize { get; init; }
        public required DateTime ModifiedTime { get; init; }
        public required string ModifiedTimeString { get; init; }

        /// <summary>ListView 显示的副标题（父目录 + 时间 + 大小）。</summary>
        public string Subtitle
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(ParentPath)) parts.Add(ParentPath);
                if (!IsDirectory)
                {
                    parts.Add(VisualSize);
                    if (!string.IsNullOrEmpty(ModifiedTimeString)) parts.Add(ModifiedTimeString);
                }
                else if (!string.IsNullOrEmpty(ModifiedTimeString))
                {
                    parts.Add(ModifiedTimeString);
                }
                return string.Join("  •  ", parts);
            }
        }

        public string IconGlyph => IsDirectory ? "\uE8B7" : "\uE7C3";
    }

    /// <summary>
    /// 集成在主程序内的文件搜索：原生 C# 实现，结果直接显示在 LRS 界面里。
    /// 支持通配符（<c>*</c> / <c>?</c>）、包含子文件夹开关、文件名 / 全路径匹配、取消。
    /// </summary>
    public partial class SearchViewModel : ViewModelBase
    {
        /// <summary>单次搜索最多收集的结果数（避免极端目录卡死 UI）。</summary>
        public const int MaxResults = 5000;

        private readonly DispatcherQueue _uiDispatcher;
        private CancellationTokenSource? _cts;

        public SearchViewModel(DispatcherQueue uiDispatcher)
        {
            _uiDispatcher = uiDispatcher;
        }

        // ---- 绑定属性 ----

        [ObservableProperty] private string _searchTerm = string.Empty;
        [ObservableProperty] private string _searchRoot = string.Empty;
        [ObservableProperty] private bool _includeSubfolders = true;
        [ObservableProperty] private bool _matchFullPath = false;
        [ObservableProperty] private bool _isSearching;
        [ObservableProperty] private string _statusText = "输入关键词开始搜索";
        [ObservableProperty] private int _resultCount;

        public ObservableCollection<SearchResultItem> Results { get; } = new();

        // 派生属性用于控制“清空”按钮 / 状态文本颜色等
        public bool HasResults => Results.Count > 0;
        public bool HasTerm => !string.IsNullOrWhiteSpace(SearchTerm);

        /// <summary>是否显示搜索中（ProgressRing 用的 Visibility）。</summary>
        public Microsoft.UI.Xaml.Visibility IsSearchingVisibility =>
            IsSearching ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        partial void OnSearchTermChanged(string value) => OnPropertyChanged(nameof(HasTerm));
        partial void OnIsSearchingChanged(bool value)
        {
            OnPropertyChanged(nameof(IsNotSearching));
            OnPropertyChanged(nameof(IsSearchingVisibility));
        }
        public bool IsNotSearching => !IsSearching;

        // ---- 入口 ----

        /// <summary>
        /// 用户在搜索框键入回车、或点 🔍 按钮时调用。
        /// 取消上一次未完成的搜索，再启动新搜索。
        /// </summary>
        public async Task SearchAsync()
        {
            // 取消上一次
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var term = (SearchTerm ?? string.Empty).Trim();
            var root = (SearchRoot ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(term))
            {
                Results.Clear();
                ResultCount = 0;
                StatusText = "输入关键词开始搜索";
                OnPropertyChanged(nameof(HasResults));
                return;
            }
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                Results.Clear();
                ResultCount = 0;
                StatusText = "搜索根目录无效";
                OnPropertyChanged(nameof(HasResults));
                return;
            }

            IsSearching = true;
            StatusText = "搜索中…";

            // 把匹配模式在后台线程编译
            Regex? nameRegex = null;
            string? nameContainsLower = null;
            string? namePatternUpper = null;
            if (term.Contains('*') || term.Contains('?'))
            {
                namePatternUpper = WildcardToRegex(term).ToUpperInvariant();
            }
            else if (MatchFullPath)
            {
                // 全路径子串匹配
            }
            else
            {
                nameContainsLower = term.ToUpperInvariant();
            }

            var includeSub = IncludeSubfolders;
            var searchOption = includeSub ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var found = new List<SearchResultItem>(64);

            try
            {
                await Task.Run(() =>
                {
                    ScanRecursive(root, term, nameRegex, nameContainsLower, namePatternUpper, MatchFullPath, searchOption, found, token);
                }, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 用户又发起了新搜索或主动取消
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                await SetStatusOnUI("无权限访问部分目录：" + ex.Message);
                return;
            }
            catch (PathTooLongException ex)
            {
                await SetStatusOnUI("路径过长：" + ex.Message);
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SearchViewModel] error: {ex}");
                await SetStatusOnUI("搜索出错：" + ex.Message);
                return;
            }

            // 把结果回填到 UI
            await _uiDispatcher.EnqueueAsync(() =>
            {
                Results.Clear();
                foreach (var item in found) Results.Add(item);
                ResultCount = found.Count;
                StatusText = found.Count == 0
                    ? "未找到匹配项"
                    : (found.Count >= MaxResults
                        ? $"已显示前 {MaxResults} 条结果（可能还有更多）"
                        : $"共 {found.Count} 条结果");
                IsSearching = false;
                OnPropertyChanged(nameof(HasResults));
            });
        }

        /// <summary>停止当前搜索（如果有）。</summary>
        public void Cancel()
        {
            _cts?.Cancel();
        }

        /// <summary>清空搜索结果与关键词。</summary>
        public void Clear()
        {
            Cancel();
            SearchTerm = string.Empty;
            Results.Clear();
            ResultCount = 0;
            StatusText = "输入关键词开始搜索";
            IsSearching = false;
            OnPropertyChanged(nameof(HasResults));
        }

        /// <summary>设置搜索根目录（一般从顶栏 / 文件树上下文取）。</summary>
        public void SetRoot(string? path)
        {
            SearchRoot = path ?? string.Empty;
        }

        // ---- 实现 ----

        private async Task SetStatusOnUI(string text)
        {
            await _uiDispatcher.EnqueueAsync(() =>
            {
                IsSearching = false;
                StatusText = text;
            });
        }

        /// <summary>
        /// 递归扫描单个目录，把匹配项加入 <paramref name="found"/>。
        /// 遇到 <see cref="OperationCanceledException"/> 直接抛出，让外层提前返回。
        /// </summary>
        private void ScanRecursive(
            string root,
            string term,
            Regex? _,
            string? nameContainsLower,
            string? namePatternUpper,
            bool matchFullPath,
            SearchOption option,
            List<SearchResultItem> found,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            // 文件
            IEnumerable<string> fileEnum;
            try
            {
                fileEnum = Directory.EnumerateFiles(root, "*", option);
            }
            catch (UnauthorizedAccessException) { fileEnum = Array.Empty<string>(); }
            catch (PathTooLongException) { fileEnum = Array.Empty<string>(); }
            catch (DirectoryNotFoundException) { fileEnum = Array.Empty<string>(); }
            catch (IOException) { fileEnum = Array.Empty<string>(); }

            foreach (var file in fileEnum)
            {
                if (token.IsCancellationRequested) throw new OperationCanceledException(token);
                if (found.Count >= MaxResults) return;
                if (MatchesFile(file, nameContainsLower, namePatternUpper, matchFullPath))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        found.Add(new SearchResultItem
                        {
                            Name = info.Name,
                            FullPath = info.FullName,
                            ParentPath = info.DirectoryName ?? string.Empty,
                            IsDirectory = false,
                            Size = info.Length,
                            VisualSize = FileSystemNodeViewModel.FormatFileSize(info.Length),
                            ModifiedTime = info.LastWriteTime,
                            ModifiedTimeString = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                        });
                    }
                    catch { /* 单个文件元数据失败不影响整体 */ }
                }
            }

            // 文件夹（仅在 includeSubfolders 时遍历；TopDirectoryOnly 已被 EnumerateFiles 处理）
            if (option == SearchOption.AllDirectories)
            {
                IEnumerable<string> dirEnum;
                try
                {
                    dirEnum = Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly);
                }
                catch (UnauthorizedAccessException) { dirEnum = Array.Empty<string>(); }
                catch (PathTooLongException) { dirEnum = Array.Empty<string>(); }
                catch (DirectoryNotFoundException) { dirEnum = Array.Empty<string>(); }
                catch (IOException) { dirEnum = Array.Empty<string>(); }

                foreach (var dir in dirEnum)
                {
                    if (token.IsCancellationRequested) throw new OperationCanceledException(token);
                    if (found.Count >= MaxResults) return;
                    if (MatchesFile(dir, nameContainsLower, namePatternUpper, matchFullPath))
                    {
                        try
                        {
                            var info = new DirectoryInfo(dir);
                            found.Add(new SearchResultItem
                            {
                                Name = info.Name,
                                FullPath = info.FullName,
                                ParentPath = info.Parent?.FullName ?? string.Empty,
                                IsDirectory = true,
                                Size = 0,
                                VisualSize = "—",
                                ModifiedTime = info.LastWriteTime,
                                ModifiedTimeString = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                            });
                        }
                        catch { }
                    }

                    try
                    {
                        ScanRecursive(dir, term, null, nameContainsLower, namePatternUpper, matchFullPath, option, found, token);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch { /* 单个子目录失败不影响整体 */ }

                    if (found.Count >= MaxResults) return;
                }
            }
        }

        private static bool MatchesFile(string path, string? nameContainsLower, string? namePatternUpper, bool matchFullPath)
        {
            try
            {
                if (namePatternUpper != null)
                {
                    // 通配符
                    var upper = matchFullPath ? path.ToUpperInvariant() : Path.GetFileName(path).ToUpperInvariant();
                    return Regex.IsMatch(upper, namePatternUpper);
                }
                if (nameContainsLower != null)
                {
                    if (matchFullPath)
                    {
                        return path.ToUpperInvariant().Contains(nameContainsLower);
                    }
                    return Path.GetFileName(path).ToUpperInvariant().Contains(nameContainsLower);
                }
            }
            catch { }
            return false;
        }

        /// <summary>把 <c>*</c>/<c>?</c> 通配符转成正则。</summary>
        private static string WildcardToRegex(string pattern)
        {
            var sb = new StringBuilder(pattern.Length * 2);
            sb.Append('^');
            foreach (var c in pattern)
            {
                if (c == '*') sb.Append(".*");
                else if (c == '?') sb.Append('.');
                else sb.Append(Regex.Escape(c.ToString()));
            }
            sb.Append('$');
            return sb.ToString();
        }
    }
}
