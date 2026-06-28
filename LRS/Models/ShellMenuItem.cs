using System.Collections.Generic;

namespace LRS.Models
{
    public class ShellMenuItem
    {
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string? IconPath { get; set; }
        public int IconIndex { get; set; } = 0;
        public List<ShellMenuItem> SubItems { get; set; } = new();
        public bool IsSeparator { get; set; } = false;
        public bool IsExtended { get; set; } = false;
        public string? MUIVerb { get; set; }
    }
}
