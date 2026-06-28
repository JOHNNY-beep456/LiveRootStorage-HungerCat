using System.Windows.Input;

namespace LRS.UserControls
{
    public class BreadcrumbSegment
    {
        public string DisplayName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsLast { get; set; }
        public ICommand NavigateCommand { get; set; } = null!;
        public ICommand NavigateSubCommand { get; set; } = null!;
    }
}
