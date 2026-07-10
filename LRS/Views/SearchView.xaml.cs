using LRS.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Threading.Tasks;
using Windows.System;

namespace LRS.Views
{
    public sealed partial class SearchView : Page
    {
        private MainWindowViewModel? VM => App.SharedViewModel;

        public SearchView()
        {
            InitializeComponent();
            DataContext = App.SharedViewModel;
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 自动聚焦搜索框
            SearchInput.Focus(FocusState.Programmatic);
        }

        private async void OnSearchClick(object sender, RoutedEventArgs e)
        {
            if (VM?.Search == null) return;
            await VM.Search.SearchAsync();
        }

        private async void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                if (VM?.Search != null)
                {
                    await VM.Search.SearchAsync();
                }
            }
            else if (e.Key == VirtualKey.Escape)
            {
                e.Handled = true;
                VM?.CloseSearch();
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            VM?.CloseSearch();
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            VM?.Search?.Clear();
            SearchInput.Focus(FocusState.Programmatic);
        }

        private void OnResultDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (ResultsList.SelectedItem is SearchResultItem item && VM != null)
            {
                _ = VM.NavigateToResultAsync(item);
            }
        }
    }
}
