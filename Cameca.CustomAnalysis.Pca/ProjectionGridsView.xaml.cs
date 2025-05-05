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
        projectionGridsView.RefreshGridData();
    }

    private void GridsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshGridData();
    }

    internal string AxisLabelForGridLetter(char gridLetter)
    { 
        return "PCA Component " + TwoDGridID.IndexForGridLetter(gridLetter);
    }

    internal string AxisYLabelForGridID(string gridID)
    {
        if (gridID.Count() != 2)
        {
            return "Unknown Pca Axis";
        }
        return AxisLabelForGridLetter(gridID[0]);
    }

    // 
    internal string AxisXLabelForGridID(string gridID)
    {
        if ( gridID.Count() != 2 )
        {
            return "Unknown Pca Axis";
        }
        return AxisLabelForGridLetter(gridID[1]);
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
            //for whatever reason, for grid PQ, we seem to have put the P in the Y axis, and the Q in the Z axis
            // so, the X axis gets its name from the second letter in the grid ID
            histogram.AxisXLabel = AxisXLabelForGridID(renderData.Name);
            histogram.AxisYLabel = AxisYLabelForGridID(renderData.Name);
            histogram.DataSource = singleList;
            histogram.IsLegendVisible = true;
            Label gridLabel = GridLabel;
            gridLabel.Content = "Grid " + renderData.Name;
            // need to hook up to "grid label provider"
            // so that we can associate the nth grid with a grid label
        }
    }

    public ICollection<IRenderData> GridsSource
    {
        get { return (ICollection<IRenderData>)GetValue(GridsSourceProperty); }
        set { 
                SetValue(GridsSourceProperty, value);
                RefreshGridData();
            }
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
