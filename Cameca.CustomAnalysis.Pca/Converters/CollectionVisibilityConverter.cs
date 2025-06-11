using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Cameca.CustomAnalysis.Pca;

internal class CollectionVisibilityConverter : MarkupExtension, IValueConverter
{
    public bool Invert { get; init; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ICollection collection)
        {
            return collection.Count > 0 ^ Invert
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
