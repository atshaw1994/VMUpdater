using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VMUpdater.Helpers
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value is bool b && b;

            // Explicitly check for "Inverse"
            if (parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            {
                boolValue = !boolValue;
            }

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = value is Visibility v && v == Visibility.Visible;

            if (parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            {
                isVisible = !isVisible;
            }

            return isVisible;
        }
    }
}
