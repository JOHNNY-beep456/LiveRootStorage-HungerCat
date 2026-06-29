using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LRS.Models
{
    /// <summary>
    /// 顶层 .hlds 清单反序列化目标。
    /// 字段命名严格遵循 README 第 6 章字段表；
    /// 必填字段缺失时由 <c>ExtensionLoader.LoadFromFile</c> 拒绝加载。
    /// </summary>
    public sealed class ExtensionManifest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("entry")]
        public string Entry { get; set; } = string.Empty;

        /// <summary>
        /// 键为扩展点名（"context_menu" / "topbar_buttons" / "file_columns" / "settings"），
        /// 值为该点贡献项的原始 JSON 数组。<see cref="ExtensionManager"/>
        /// 在加载时按需反序列化为强类型 <see cref="ExtensionPoint"/> 子类。
        /// </summary>
        [JsonPropertyName("points")]
        public Dictionary<string, JsonElement> Points { get; set; } = new();

        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }
}
