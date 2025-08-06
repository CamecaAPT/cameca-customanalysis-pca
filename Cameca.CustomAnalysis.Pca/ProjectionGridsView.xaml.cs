using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Pca.Utils;
using Cameca.CustomAnalysis.Pca.VoxelLogic;
using Cameca.Extensions.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Cameca.CustomAnalysis.Pca;

/// <summary>
/// Interaction logic for ProjectionGridsView.xaml
/// </summary>
public partial class ProjectionGridsView : UserControl
{
   // [ObservableProperty]
    private bool mouseHovering;
    Histogram2D? currentTintedHistogram;
    Histogram2D? currentUntintedHistogram;
    string currentGridId = "";
    readonly HashSet<string> gridsToIncludeForPCAPhaseID = new();
    int whichGrid = 0;
    public ProjectionGridsView()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty GridsSourceTintedProperty = DependencyProperty.Register(
        nameof(GridsSourceTinted),
        typeof(ICollection<IRenderData>),
        typeof(ProjectionGridsView),
        new FrameworkPropertyMetadata(Array.Empty<IRenderData>(), GridsSourceTintedPropertyChanged));

    public static readonly DependencyProperty GridsSourceUntintedProperty = DependencyProperty.Register(
        nameof(GridsSourceUntinted),
        typeof(ICollection<IRenderData>),
        typeof(ProjectionGridsView),
        new FrameworkPropertyMetadata(Array.Empty<IRenderData>(), GridsSourceUntintedPropertyChanged));

    // GridsUsageDelegate is an object that can accept notifications of whether or not to use a particular grid as 
    // part of the PCA Phase identification process.  So, when the "Use this grid in PCA Phase ID" button is clicked
    // this object will get notified of the user intent.
    public static readonly DependencyProperty GridsUsageDelegateProperty = DependencyProperty.Register(
            nameof(GridsUsageDelegate),
            typeof(IGridsUsageDelegate),
            typeof(ProjectionGridsView),
            new FrameworkPropertyMetadata(new DoNothingGridsUsageDelegate(), GridsUsageDelegatePropertyChanged));

    // GridsUsageProtocol is an object that can accept notifications of whether o not to use a particular grid as 
    // part of the PCA Phase identification process.  So, when the "Use this grid in PCA Phase ID" button is clicked
    // this object will get notified of the user intent.
    public static readonly DependencyProperty GridsColorMapProviderProperty = DependencyProperty.Register(
            nameof(GridsColorMapProvider),
            typeof(IGridsColorMapProvider),
            typeof(ProjectionGridsView),
            new FrameworkPropertyMetadata(new PcaGridsColorMapProvider(null), GridsColorMapProviderPropertyChanged));

    private static void GridsUsageDelegatePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
    }
    private static void GridsColorMapProviderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
    }

    private static void GridsSourceTintedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ProjectionGridsView projectionGridsView) { return; }
        projectionGridsView.RefreshGridData();
    }

    private static void GridsSourceUntintedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ProjectionGridsView projectionGridsView) { return; }
        projectionGridsView.RefreshGridData();
    }

    private void GridsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshGridData();
    }

    static internal string AxisLabelForGridLetter(char gridLetter)
    { 
        return "PCA Component " + GridID.IndexForGridLetter(gridLetter);
    }

    static internal string AxisYLabelForGridID(string gridID)
    {
        if (gridID.Length != 2)
        {
            return "Unknown Pca Axis";
        }
        return AxisLabelForGridLetter(gridID[0]);
    }

    // 
    static internal string AxisXLabelForGridID(string gridID)
    {
        if (gridID.Length != 2 )
        {
            return "Unknown Pca Axis";
        }
        return AxisLabelForGridLetter(gridID[1]);
    }


    private void RefreshGridData()
    {
        ICollection<IRenderData> renderDataCollectionTinted = this.GridsSourceTinted;
        ICollection<IRenderData> renderDataCollectionUntinted = this.GridsSourceUntinted;
        int rdc = renderDataCollectionTinted.Count;
 
        if (rdc > 0)
        {
            List<IRenderData> renderListTinted = renderDataCollectionTinted.ToList();
            List<IRenderData> renderListUntinted = renderDataCollectionUntinted.ToList();

            if (whichGrid >= rdc)
            {
                whichGrid = 0;
            }
            if (whichGrid < 0)
            {
                whichGrid = rdc - 1;
            }

            if (renderListTinted.Count > whichGrid) {
                var renderDataTinted = renderListTinted[whichGrid];
                currentGridId = renderDataTinted.Name;
                List<IRenderData> singleListTinted = new List<IRenderData> { renderDataTinted };
                Histogram2D histogramTinted = ProjectionGrid2dHistogramTinted;
                histogramTinted.AxisXLabel = AxisXLabelForGridID(renderDataTinted.Name);
                histogramTinted.AxisYLabel = AxisYLabelForGridID(renderDataTinted.Name);
                histogramTinted.DataSource = singleListTinted;
                histogramTinted.IsLegendVisible = true;
                histogramTinted.Visibility = Visibility.Collapsed;
                currentTintedHistogram = histogramTinted;
                Label gridLabel = GridLabel;
                gridLabel.Content = "Grid " + renderDataTinted.Name;
            }
            if (renderListUntinted.Count > whichGrid)
            {
                var renderDataUntinted = renderListUntinted[whichGrid];
                currentGridId = renderDataUntinted.Name;
                List<IRenderData> singleListUntinted = new List<IRenderData> { renderDataUntinted };
                Histogram2D histogramUntinted = ProjectionGrid2dHistogramUntinted;
                //for whatever reason, for grid PQ, we seem to have put the P in the Y axis, and the Q in the Z axis
                // so, the X axis gets its name from the second letter in the grid ID

                histogramUntinted.AxisXLabel = AxisXLabelForGridID(renderDataUntinted.Name);
                histogramUntinted.AxisYLabel = AxisYLabelForGridID(renderDataUntinted.Name);
                histogramUntinted.DataSource = singleListUntinted;
                histogramUntinted.IsLegendVisible = true;
                histogramUntinted.Visibility = Visibility.Visible;
                Label gridLabel = GridLabel;
                gridLabel.Content = "Grid " + renderDataUntinted.Name;
                currentUntintedHistogram = histogramUntinted;
            }

            UseGridForPCAPhaseID.IsChecked = GridsUsageDelegate.UsesGridForPca(currentGridId);
            GridInfoText.Text = GridsUsageDelegate.GridInfo(currentGridId);
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

    public IGridsColorMapProvider GridsColorMapProvider
    {
        get { return (IGridsColorMapProvider)GetValue(GridsColorMapProviderProperty); }
        set
        {
            SetValue(GridsColorMapProviderProperty, value);
        }
    }

    public ICollection<IRenderData> GridsSourceTinted
    {
        get { return (ICollection<IRenderData>)GetValue(GridsSourceTintedProperty); }
        set
        {
            SetValue(GridsSourceTintedProperty, value);
            RefreshGridData();
        }
    }
    public ICollection<IRenderData> GridsSourceUntinted
    {
        get { return (ICollection<IRenderData>)GetValue(GridsSourceUntintedProperty); }
        set
        {
            SetValue(GridsSourceUntintedProperty, value);
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
    private void ProjectionGrid2dHistogram_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Histogram2D histogram)
        {
            if (histogram == currentTintedHistogram)
            {
                currentTintedHistogram.Visibility = Visibility.Collapsed;
                currentUntintedHistogram.Visibility = Visibility.Visible;
            }
        }
    }
    private void ProjectionGrid2dHistogram_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Histogram2D histogram)
        {
            if (histogram == currentUntintedHistogram)
            {
                currentTintedHistogram.Visibility = Visibility.Visible;
                currentUntintedHistogram.Visibility = Visibility.Collapsed;
            }
        }
    }
}
