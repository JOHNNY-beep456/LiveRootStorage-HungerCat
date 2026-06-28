using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace LRS.UserControls
{
    public class LastToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool isLast && isLast) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
