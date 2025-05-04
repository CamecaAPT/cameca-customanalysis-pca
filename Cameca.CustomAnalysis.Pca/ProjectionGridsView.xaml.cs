using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using Cameca.Extensions.Controls;
using CommunityToolkit.HighPerformance;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

namespace Cameca.CustomAnalysis.Pca;

/// <summary>
/// Interaction logic for ProjectionGridsView.xaml
/// </summary>
public partial class ProjectionGridsView : UserControl
{

    int whichGrid = 0;
    public ProjectionGridsView()
    {
        InitializeComponent();
        var histogram = ProjectionGrid2dHistogram;
    }

    public static readonly DependencyProperty GridsSourceProperty = DependencyProperty.Register(
        nameof(GridsSource),
        typeof(ICollection<IRenderData>),
        typeof(ProjectionGridsView),
        new FrameworkPropertyMetadata(Array.Empty<IRenderData>(), GridsSourcePropertyChanged));

    private static void GridsSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ProjectionGridsView projectionGridsView) { return; }
        ICollection<IRenderData> renderData = projectionGridsView.GridsSource;
        int rdc = renderData.Count;
    }

    private void GridsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshGridData();
    }

    internal string AxisYLabelForGridID(string gridID)
    {
        string[] components = gridID.Split("-");
        if (components.Count() != 2)
        {
            return "Unknown Pca Axis";
        }
        string component = components[0];
        return "PCA Component " + component.Substring(1);
    }
    internal string AxisXLabelForGridID(string gridID)
    {
        string[] components = gridID.Split("-");
        if (components.Count() != 2)
        {
            return "Unknown Pca Axis";
        }
        string component = components[1];
        return "PCA Component " + component;
    }


    private void RefreshGridData()
    { 
        ICollection<IRenderData> renderDataCollection = this.GridsSource;
        int rdc = renderDataCollection.Count;
 
        if (rdc > 0)
        {
            List<IRenderData> renderList = renderDataCollection.ToList();
            if (whichGrid >= rdc)
            {
                whichGrid = 0;
            }
            if (whichGrid < 0)
            {
                whichGrid = rdc - 1;
            }

            var renderData = renderList[whichGrid];
            List<IRenderData> singleList = new List<IRenderData> { renderData };
            Histogram2D histogram = ProjectionGrid2dHistogram;
            histogram.AxisXLabel = AxisXLabelForGridID(renderData.Name);
            histogram.AxisYLabel = AxisYLabelForGridID(renderData.Name);
            histogram.DataSource = singleList;
            histogram.IsLegendVisible = true;
            Label gridLabel = GridLabel;
            gridLabel.Content = "Grid Index " + whichGrid;
            // need to hook up to "grid label provider"
            // so that we can associate the nth grid with a grid label
        }
    }

    public ICollection<IRenderData> GridsSource
    {
        get { return (ICollection<IRenderData>)GetValue(GridsSourceProperty); }
        set { SetValue(GridsSourceProperty, value); }
    }

    private void AdvanceGridButton_Click(object sender, RoutedEventArgs e)
    {
        whichGrid += 1;
        RefreshGridData();
    }
    private void PreviousGridButton_Click(object sender, RoutedEventArgs e)
    {
        whichGrid -= 1;
        RefreshGridData();
    }

    private static IEnumerable<T> GetChildren<T>(DependencyObject root) where T: DependencyObject
    {
        int childrenCount = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T)
            {
                yield return (T)child;
            }
            foreach (var recursiveChild in GetChildren<T>(child))
            {
                yield return recursiveChild;
            }
        }
    }
}
