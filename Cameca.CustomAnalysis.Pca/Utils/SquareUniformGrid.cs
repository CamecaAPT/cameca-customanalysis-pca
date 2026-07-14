using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace Cameca.CustomAnalysis.Pca;

public class SquareUniformGrid : UniformGrid
{
    protected override Size MeasureOverride(Size constraint)
    {
        int componentCount = InternalChildren.Cast<UIElement>().Count(c => c.Visibility != Visibility.Collapsed);
        double root = Math.Sqrt(componentCount);
        Columns = (int)Math.Ceiling(root);
        Rows = Columns > 0 ? (componentCount / Columns) + (componentCount % Columns > 0 ? 1 : 0) : 0;
        return base.MeasureOverride(constraint);
    }
}
