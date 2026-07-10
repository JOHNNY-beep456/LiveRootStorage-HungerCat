using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using LRS.Models;

namespace LRS.Services
{
    /// <summary>
    /// 负责把磁盘上的 <c>.hlds</c> 文件反序列化为 <see cref="ExtensionManifest"/>。
    /// <para>
    /// <b>设计原则</b>：所有异常都被吞掉并记录到 <c>Debug.WriteLine</c>，返回 <c>null</c>，
    /// 这样上层 <see cref="ExtensionManager"/> 在扫描一个目录时不会因为单个坏文件
    /// 而中断整个初始化流程（失败隔离）。
    /// </para>
    /// </summary>
    public static class ExtensionLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>
        /// 尝试加载单个 <c>.hlds</c>。失败原因通过 <see cref="Debug.WriteLine"/> 输出。
        /// </summary>
        /// <param name="path">绝对路径。</param>
        /// <returns>合法清单，或 <c>null</c>（文件不存在 / IO 错误 / JSON 错误 / 字段校验失败）。</returns>
        public static ExtensionManifest? LoadFromFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!File.Exists(path)) return null;

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExtensionLoader] Read failed: {path} -> {ex.Message}");
                return null;
            }

            ExtensionManifest? manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<ExtensionManifest>(text, JsonOptions);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[ExtensionLoader] JSON parse failed: {path} -> {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExtensionLoader] Deserialize failed: {path} -> {ex.Message}");
                return null;
            }

            if (manifest == null)
            {
                Debug.WriteLine($"[ExtensionLoader] Manifest is null: {path}");
                return null;
            }

            // 必填字段校验
            if (string.IsNullOrWhiteSpace(manifest.Id))
            {
                Debug.WriteLine($"[ExtensionLoader] Missing 'id': {path}");
                return null;
            }
            if (manifest.Id.Contains(':'))
            {
                Debug.WriteLine($"[ExtensionLoader] 'id' must not contain ':': {manifest.Id}");
                return null;
            }
            if (string.IsNullOrWhiteSpace(manifest.Name))
            {
                Debug.WriteLine($"[ExtensionLoader] Missing 'name': {path}");
                return null;
            }
            if (string.IsNullOrWhiteSpace(manifest.Version))
            {
                Debug.WriteLine($"[ExtensionLoader] Missing 'version': {path}");
                return null;
            }
            if (string.IsNullOrWhiteSpace(manifest.Type))
            {
                Debug.WriteLine($"[ExtensionLoader] Missing 'type': {path}");
                return null;
            }
            if (string.IsNullOrWhiteSpace(manifest.Entry))
            {
                Debug.WriteLine($"[ExtensionLoader] Missing 'entry': {path}");
                return null;
            }

            return manifest;
        }

        /// <summary>
        /// 将 <see cref="ExtensionContext"/> 中的占位符 <c>%F</c> / <c>%D</c> / <c>%L</c>
        /// 替换为实际值。空值替换为空串。
        /// </summary>
        public static string SubstitutePlaceholders(string template, ExtensionContext ctx)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            var f = ctx.File ?? string.Empty;
            var d = ctx.Directory ?? string.Empty;
            var l = ctx.ListJoined ?? string.Empty;
            return template
                .Replace("%F", f)
                .Replace("%D", d)
                .Replace("%L", l);
        }
    }
}
