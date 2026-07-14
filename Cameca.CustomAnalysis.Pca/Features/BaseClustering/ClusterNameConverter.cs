using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Cameca.CustomAnalysis.Pca;

internal class ClusterNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2
            && values[0] is string id
            && values[1] is IReadOnlyDictionary<string, string> map
            && map.TryGetValue(id, out var mappedName))
        {
            return mappedName;
        }
        if (values.Length > 0 && values[0] is string fallbackId)
        {
            return fallbackId;
        }
        return DependencyProperty.UnsetValue;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
