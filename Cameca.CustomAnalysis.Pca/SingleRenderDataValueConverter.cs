using Cameca.CustomAnalysis.Interface;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Cameca.CustomAnalysis.Pca;

internal class SingleRenderDataValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is IRenderData renderData)
        {
            return new IRenderData[] { renderData };
        }
        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
