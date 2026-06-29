using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LRS.Models;
using Microsoft.UI.Dispatching;

namespace LRS.Services
{
    /// <summary>
    /// 扫描 <c>./ext/</c> 与 <c>AppContext.BaseDirectory/ext/</c> 下的所有 <c>.hlds</c>，
    /// 向主程序暴露按类型聚合的贡献、命令执行、启用 / 禁用与热重载事件。
    /// </summary>
    /// <remarks>
    /// 加载源固定为运行时输出目录（<see cref="AppContext.BaseDirectory"/>/ext/）。
    /// <c>.csproj</c> 通过 <c>&lt;None Include="..\ext\**\*.hlds" ...&gt;</c> 把
    /// 仓库根的 <c>ext/</c> 拷贝到输出，因此 dev 与 prod 走同一条路径。
    /// </remarks>
    public sealed class ExtensionManager : IDisposable
    {
        /// <summary>进程退出码与错误流结果。</summary>
        public sealed record ExecutionResult(int ExitCode, string? Error);

        /// <summary>已加载清单的不可变快照。</summary>
        public sealed record LoadedExtension(
            string Id,
            string Name,
            string Version,
            string Author,
            string Description,
            string SourcePath,
            ExtensionManifest Manifest);

        /// <summary>类型 → 扩展点名 的映射（<see cref="GetContributions{T}"/> 内部使用）。</summary>
        private static readonly IReadOnlyDictionary<Type, string> PointNameByType =
            new Dictionary<Type, string>
            {
                [typeof(ContextMenuContribution)] = "context_menu",
                [typeof(TopbarButtonContribution)] = "topbar_buttons",
                [typeof(FileColumnContribution)] = "file_columns",
                [typeof(SettingContribution)] = "settings",
            };

        private readonly string _extDirectory;
        private DispatcherQueue? _uiDispatcher;
        private readonly System.Timers.Timer _debounceTimer;
        private FileSystemWatcher? _watcher;
        private readonly SemaphoreSlim _reloadLock = new(1, 1);

        private readonly Dictionary<string, LoadedExtension> _loaded = new(StringComparer.Ordinal);
        private readonly HashSet<string> _disabledIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (string ExtensionId, string Command)> _commandLookup =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _manifestErrors = new(StringComparer.Ordinal);
        private bool _disposed;

        /// <summary>热重载 / 启用状态变化后触发。订阅方应 marshal 回 UI 线程。</summary>
        public event EventHandler? Changed;

        public ExtensionManager() : this(uiDispatcher: null) { }

        public ExtensionManager(DispatcherQueue? uiDispatcher)
        {
            _uiDispatcher = uiDispatcher;
            _extDirectory = Path.Combine(AppContext.BaseDirectory, "ext");

            _debounceTimer = new System.Timers.Timer(250)
            {
                AutoReset = false,
            };
            _debounceTimer.Elapsed += (_, _) => _ = ReloadAsync();
        }

        /// <summary>
        /// 注入 UI 线程的 <see cref="DispatcherQueue"/>，用于把 <see cref="Changed"/>
        /// 事件 marshal 回 UI 线程。在 <c>OnLaunched</c> 中调用。
        /// </summary>
        public void SetDispatcher(DispatcherQueue dispatcher)
        {
            _uiDispatcher = dispatcher;
        }

        /// <summary>扩展扫描目录（运行时）。仅用于诊断 / 日志。</summary>
        public string ExtensionDirectory => _extDirectory;

        /// <summary>当前已加载（且未禁用）的扩展清单，按 <c>Id</c> 排序。</summary>
        public IReadOnlyList<LoadedExtension> LoadedExtensions =>
            _loaded.Values.OrderBy(e => e.Id, StringComparer.Ordinal).ToList();

        /// <summary>最近一次扫描中各文件的错误信息，键为绝对路径。</summary>
        public IReadOnlyDictionary<string, List<string>> ManifestErrors => _manifestErrors;

        /// <summary>
        /// 启动初始化：扫描目录 + 启动 <see cref="FileSystemWatcher"/>。
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await ReloadAsync(cancellationToken).ConfigureAwait(false);
            StartWatcher();
        }

        /// <summary>重载：重新扫描目录并刷新内部缓存。</summary>
        public async Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) return;
            await _reloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!Directory.Exists(_extDirectory))
                {
                    try { Directory.CreateDirectory(_extDirectory); }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ExtensionManager] mkdir failed: {ex.Message}");
                    }
                }

                var newLoaded = new Dictionary<string, LoadedExtension>(StringComparer.Ordinal);
                var newErrors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

                if (Directory.Exists(_extDirectory))
                {
                    foreach (var file in Directory.EnumerateFiles(_extDirectory, "*.hlds", SearchOption.TopDirectoryOnly))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var manifest = ExtensionLoader.LoadFromFile(file);
                        if (manifest == null)
                        {
                            newErrors[file] = new List<string> { "load failed (see Debug output for details)" };
                            continue;
                        }
                        if (newLoaded.ContainsKey(manifest.Id))
                        {
                            newErrors[file] = new List<string>
                            {
                                $"duplicate id '{manifest.Id}' (also in {newLoaded[manifest.Id].SourcePath})"
                            };
                            continue;
                        }
                        newLoaded[manifest.Id] = new LoadedExtension(
                            manifest.Id,
                            manifest.Name,
                            manifest.Version,
                            manifest.Author,
                            manifest.Description,
                            file,
                            manifest);
                    }
                }

                _loaded.Clear();
                foreach (var kv in newLoaded) _loaded[kv.Key] = kv.Value;
                _manifestErrors.Clear();
                foreach (var kv in newErrors) _manifestErrors[kv.Key] = kv.Value;

                RebuildCommandLookup();
                RaiseChanged();
            }
            finally
            {
                _reloadLock.Release();
            }
        }

        /// <summary>获取指定类型的全部贡献（仅包含已启用扩展）。</summary>
        public IReadOnlyList<T> GetContributions<T>() where T : class
        {
            return GetTypedContributions<T>().Select(x => x.Contribution).ToList();
        }

        /// <summary>
        /// 与 <see cref="GetContributions{T}"/> 类似，但额外返回贡献所属的扩展 ID，
        /// 供 UI 在派发点击事件时定位扩展。
        /// </summary>
        public IReadOnlyList<(string ExtensionId, T Contribution)> GetTypedContributions<T>() where T : class
        {
            if (!PointNameByType.TryGetValue(typeof(T), out var pointName))
                throw new ArgumentException($"Unsupported contribution type: {typeof(T).Name}");

            var results = new List<(string, T)>();
            foreach (var ext in _loaded.Values)
            {
                if (_disabledIds.Contains(ext.Id)) continue;
                if (!ext.Manifest.Points.TryGetValue(pointName, out var pointElem)) continue;
                if (pointElem.ValueKind != JsonValueKind.Array) continue;

                T[] items;
                try
                {
                    items = JsonSerializer.Deserialize<T[]>(pointElem.GetRawText())
                            ?? Array.Empty<T>();
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"[ExtensionManager] point '{pointName}' of '{ext.Id}' invalid: {ex.Message}");
                    continue;
                }
                foreach (var item in items) results.Add((ext.Id, item));
            }
            return results;
        }

        /// <summary>当前被禁用的扩展 ID 列表。</summary>
        public IReadOnlyCollection<string> GetDisabledIds() =>
            _disabledIds.ToArray();

        /// <summary>设置扩展启用状态（立即生效，并触发 <see cref="Changed"/>）。</summary>
        public void SetEnabled(string extensionId, bool enabled)
        {
            if (string.IsNullOrEmpty(extensionId)) return;
            var changed = enabled ? _disabledIds.Remove(extensionId) : _disabledIds.Add(extensionId);
            if (changed)
            {
                RebuildCommandLookup();
                RaiseChanged();
            }
        }

        /// <summary>供 <see cref="Configs"/> 初始化时把磁盘上的禁用集合灌进来。</summary>
        public void LoadDisabledIds(IEnumerable<string> ids)
        {
            _disabledIds.Clear();
            foreach (var id in ids ?? Array.Empty<string>())
            {
                if (!string.IsNullOrEmpty(id)) _disabledIds.Add(id);
            }
        }

        /// <summary>
        /// 在已启用扩展中查找 <paramref name="commandId"/> 对应的命令并执行。
        /// 找不到时返回 <c>(int.MinValue, "command not found")</c>。
        /// </summary>
        public async Task<ExecutionResult> ExecuteAsync(
            string extensionId,
            string commandId,
            ExtensionContext ctx,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(commandId))
                return new ExecutionResult(int.MinValue, "commandId is empty");
            if (string.IsNullOrEmpty(extensionId) || !_loaded.TryGetValue(extensionId, out var ext))
                return new ExecutionResult(int.MinValue, $"extension '{extensionId}' not loaded");
            if (_disabledIds.Contains(extensionId))
                return new ExecutionResult(int.MinValue, $"extension '{extensionId}' is disabled");

            // 从该扩展的全部 point 中扫描 commandId
            foreach (var (_, pointElem) in ext.Manifest.Points)
            {
                if (pointElem.ValueKind != JsonValueKind.Array) continue;
                JsonElement[] arr;
                try
                {
                    arr = JsonSerializer.Deserialize<JsonElement[]>(pointElem.GetRawText()) ?? Array.Empty<JsonElement>();
                }
                catch (JsonException) { continue; }

                foreach (var item in arr)
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    if (!item.TryGetProperty("id", out var idProp)) continue;
                    if (idProp.GetString() != commandId) continue;
                    if (!item.TryGetProperty("command", out var cmdProp)) continue;
                    var commandTemplate = cmdProp.GetString() ?? string.Empty;
                    return await RunProcessAsync(extensionId, commandTemplate, ctx, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            return new ExecutionResult(int.MinValue, $"command '{commandId}' not found in extension '{extensionId}'");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _debounceTimer.Stop();
            _debounceTimer.Dispose();
            _watcher?.Dispose();
            _watcher = null;
            _reloadLock.Dispose();
        }

        // ---- internals ----

        private void StartWatcher()
        {
            try
            {
                if (!Directory.Exists(_extDirectory))
                {
                    Directory.CreateDirectory(_extDirectory);
                }
                _watcher?.Dispose();
                _watcher = new FileSystemWatcher(_extDirectory, "*.hlds")
                {
                    NotifyFilter = NotifyFilters.FileName
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Size
                                 | NotifyFilters.CreationTime,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true,
                };
                _watcher.Created += OnFsChange;
                _watcher.Changed += OnFsChange;
                _watcher.Deleted += OnFsChange;
                _watcher.Renamed += OnFsChange;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExtensionManager] watcher start failed: {ex.Message}");
            }
        }

        private void OnFsChange(object sender, FileSystemEventArgs e)
        {
            // 防抖：250ms 内的多次事件合并为一次重载
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void RebuildCommandLookup()
        {
            _commandLookup.Clear();
            foreach (var ext in _loaded.Values)
            {
                if (_disabledIds.Contains(ext.Id)) continue;
                foreach (var (_, pointElem) in ext.Manifest.Points)
                {
                    if (pointElem.ValueKind != JsonValueKind.Array) continue;
                    JsonElement[] arr;
                    try
                    {
                        arr = JsonSerializer.Deserialize<JsonElement[]>(pointElem.GetRawText())
                              ?? Array.Empty<JsonElement>();
                    }
                    catch (JsonException) { continue; }
                    foreach (var item in arr)
                    {
                        if (item.ValueKind != JsonValueKind.Object) continue;
                        if (!item.TryGetProperty("id", out var idProp)) continue;
                        if (!item.TryGetProperty("command", out var cmdProp)) continue;
                        var id = idProp.GetString();
                        var cmd = cmdProp.GetString();
                        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(cmd)) continue;
                        // 不同扩展的同一 commandId 时，后者覆盖前者；日志提示
                        if (_commandLookup.ContainsKey(id))
                        {
                            Debug.WriteLine($"[ExtensionManager] duplicate command id '{id}' between extensions");
                        }
                        _commandLookup[id] = (ext.Id, cmd);
                    }
                }
            }
        }

        private void RaiseChanged()
        {
            var handler = Changed;
            if (handler == null) return;
            if (_uiDispatcher != null && !_uiDispatcher.HasThreadAccess)
            {
                _uiDispatcher.TryEnqueue(() => handler(this, EventArgs.Empty));
            }
            else
            {
                handler(this, EventArgs.Empty);
            }
        }

        private static async Task<ExecutionResult> RunProcessAsync(
            string extensionId,
            string commandTemplate,
            ExtensionContext ctx,
            CancellationToken cancellationToken)
        {
            var resolved = ExtensionLoader.SubstitutePlaceholders(commandTemplate, ctx);
            if (string.IsNullOrWhiteSpace(resolved))
                return new ExecutionResult(int.MinValue, "empty command after substitution");

            // 简单拆分：第一段作 FileName，其余作 Arguments
            string fileName;
            string arguments;
            var firstSpace = resolved.IndexOf(' ');
            if (firstSpace < 0)
            {
                fileName = resolved.Trim();
                arguments = string.Empty;
            }
            else
            {
                fileName = resolved.Substring(0, firstSpace).Trim();
                arguments = resolved.Substring(firstSpace + 1).TrimStart();
            }

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            try
            {
                using var proc = new Process { StartInfo = psi, EnableRaisingEvents = false };
                if (!proc.Start())
                {
                    return new ExecutionResult(int.MinValue, $"failed to start '{fileName}'");
                }
                var stderrTask = proc.StandardError.ReadToEndAsync();
                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                var stderr = await stderrTask.ConfigureAwait(false);
                var stdout = await stdoutTask.ConfigureAwait(false);
                var errText = string.IsNullOrWhiteSpace(stderr) ? null : stderr.Trim();
                if (proc.ExitCode != 0 && string.IsNullOrEmpty(errText) && !string.IsNullOrWhiteSpace(stdout))
                {
                    errText = stdout.Trim();
                }
                Debug.WriteLine($"[Extension:{extensionId}] exit={proc.ExitCode} stdout={stdout.Length} stderr={stderr.Length}");
                return new ExecutionResult(proc.ExitCode, errText);
            }
            catch (Exception ex)
            {
                return new ExecutionResult(int.MinValue, ex.Message);
            }
        }
    }
}
