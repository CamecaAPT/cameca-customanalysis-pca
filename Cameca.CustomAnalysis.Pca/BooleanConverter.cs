using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Cameca.CustomAnalysis.Pca;

internal class GeneralBooleanToVisibilityConverter : IValueConverter
{
    public Visibility True { get; set; } = Visibility.Visible;
    public Visibility False { get; set; } = Visibility.Collapsed;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool bValue && bValue ? True : False;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility vValue && vValue == True;
    }
}
