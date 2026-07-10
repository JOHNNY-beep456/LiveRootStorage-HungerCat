using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LRS.Models
{
    /// <summary>
    /// 命令执行时的运行时上下文，承载 <c>%F</c> / <c>%D</c> / <c>%L</c> 占位符的取值。
    /// </summary>
    /// <param name="File">当前项路径（<c>%F</c>）。</param>
    /// <param name="Directory">当前目录（<c>%D</c>）。</param>
    /// <param name="List">多选时的分号分隔路径串（<c>%L</c>）。允许为 <c>null</c>。</param>
    public sealed record ExtensionContext(
        string? File,
        string? Directory,
        IReadOnlyList<string>? List)
    {
        /// <summary>将 <see cref="List"/> 用分号连接为单一字符串。</summary>
        public string? ListJoined => List is null || List.Count == 0
            ? null
            : string.Join(';', List);
    }

    /// <summary>
    /// 右键菜单项的注入位置过滤。
    /// </summary>
    public enum ContextMenuApplyTo
    {
        /// <summary>任意位置都注入（默认）。</summary>
        Any,
        /// <summary>仅在文件项上注入。</summary>
        File,
        /// <summary>仅在文件夹项上注入。</summary>
        Folder,
        /// <summary>仅在空白区域右键时注入。</summary>
        Background,
    }

    /// <summary>
    /// <c>points.context_menu</c> 的单条贡献。
    /// <see cref="Command"/> 字符串可包含占位符 <c>%F</c> / <c>%D</c> / <c>%L</c>。
    /// </summary>
    public sealed class ContextMenuContribution
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("applyTo")]
        public ContextMenuApplyTo ApplyTo { get; set; } = ContextMenuApplyTo.Any;

        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;
    }

    /// <summary>
    /// <c>points.topbar_buttons</c> 的单条贡献。
    /// </summary>
    public sealed class TopbarButtonContribution
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        /// <summary>顶栏插入位置，值越小越靠左；缺省时按扩展 <c>id</c> 字典序。</summary>
        [JsonPropertyName("position")]
        public int Position { get; set; } = 100;
    }

    /// <summary>
    /// <c>points.file_columns</c> 的单条贡献。
    /// <see cref="Value"/> 必须是预定义表达式（<c>length</c> / <c>modified</c> /
    /// <c>created</c> / <c>extension</c> / <c>name</c> / <c>path</c> /
    /// <c>isfile</c> / <c>type</c>），不支持任意运行时表达式。
    /// </summary>
    public sealed class FileColumnContribution
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("header")]
        public string Header { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        /// <summary>列宽（相对权重），缺省 <c>1.0</c>。</summary>
        [JsonPropertyName("width")]
        public double Width { get; set; } = 1.0;
    }

    /// <summary>
    /// <c>points.settings</c> 的单条贡献。
    /// <see cref="Type"/> 支持 <c>toggle</c> / <c>number</c> / <c>text</c> / <c>combo</c>。
    /// </summary>
    public sealed class SettingContribution
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        /// <summary><c>toggle</c> | <c>number</c> | <c>text</c> | <c>combo</c>。</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "text";

        [JsonPropertyName("default")]
        public string Default { get; set; } = string.Empty;

        /// <summary><c>combo</c> 类型的候选项；其他类型忽略。</summary>
        [JsonPropertyName("options")]
        public List<string> Options { get; set; } = new();
    }
}
