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
    string currentGridId = "";
    readonly HashSet<string> gridsToIncludeForPCAPhaseID = new HashSet<string>();
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

    // GridsUsageProtocol is an object that can accept notifications of whether o not to use a particular grid as 
    // part of the PCA Phase identification process.  So, when the "Use this grid in PCA Phase ID" button is clicked
    // this object will get notified of the user intent.
    public static readonly DependencyProperty GridsUsageDelegateProperty = DependencyProperty.Register(
            nameof(GridsUsageDelegate),
            typeof(IGridsUsageDelegate),
            typeof(ProjectionGridsView),
            new FrameworkPropertyMetadata(new DoNothingGridsUsageDelegate(), GridsUsageDelegatePropertyChanged));

    private static void GridsUsageDelegatePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
    }

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
        return "PCA Component " + GridID.IndexForGridLetter(gridLetter);
    }

    internal string AxisYLabelForGridID(string gridID)
    {
        if (gridID.Length != 2)
        {
            return "Unknown Pca Axis";
        }
        return AxisLabelForGridLetter(gridID[0]);
    }

    // 
    internal string AxisXLabelForGridID(string gridID)
    {
        if (gridID.Length != 2 )
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
            currentGridId = renderData.Name;
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

            UseGridForPCAPhaseID.IsChecked = GridsUsageDelegate.UsesGridForPca(currentGridId);
        }
    }

    public IGridsUsageDelegate GridsUsageDelegate
    {
        get { return (IGridsUsageDelegate)GetValue(GridsUsageDelegateProperty); }
        set
        {
            SetValue(GridsUsageDelegateProperty, value);
        }
    }

    public ICollection<IRenderData> GridsSource
    {
        get { return (ICollection<IRenderData>)GetValue(GridsSourceProperty); }
        set
        {
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
    private void UseGridForPCAPhaseID_Click(object sender, RoutedEventArgs e)
    {
        CheckBox checkBox = (CheckBox)sender;
        bool isChecked = checkBox.IsChecked ?? true; 
        GridsUsageDelegate.UseGridForPca(currentGridId, isChecked);
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
