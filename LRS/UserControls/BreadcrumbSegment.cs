namespace LRS.UserControls
{
    public sealed class BreadcrumbSegment
    {
        public string DisplayName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsLast { get; set; }
        public System.Windows.Input.ICommand NavigateCommand { get; set; } = null!;
        public System.Windows.Input.ICommand NavigateSubCommand { get; set; } = null!;
    }
}
