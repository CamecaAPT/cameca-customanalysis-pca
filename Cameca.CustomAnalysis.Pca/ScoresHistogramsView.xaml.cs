using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using Cameca.Extensions.Controls;
using CommunityToolkit.HighPerformance;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using System.Windows.Media;

namespace Cameca.CustomAnalysis.Pca;

/// <summary>
/// Interaction logic for ScoresHistogramsView.xaml
/// This is the pane which shows a histogram of number of voxels with a particular PCA score in a PCA dimension
/// And can cycle through the different grids with the Advance / Move Back buttons
/// There is an (as yet unused) button for 'use this dimension in the PCA Phase determination'
/// </summary>
public partial class ScoresHistogramsView : UserControl
{
    string currentHistogramId = "";
    HashSet<string> histogramsToUseForPCAPhaseID = new HashSet<string>();
    int whichHistogram = 0;
    public ScoresHistogramsView()
    {
        InitializeComponent();
        var histogram = ScoresHistogram;
    }

    public static readonly DependencyProperty HistogramsSourceProperty = DependencyProperty.Register(
        nameof(HistogramsSource),
        typeof(ICollection<IRenderData>),
        typeof(ScoresHistogramsView),
        new FrameworkPropertyMetadata(Array.Empty<IRenderData>(), HistogramsSourcePropertyChanged));

    // GridsUsageProtocol is an object that can accept notifications of whether o not to use a particular grid as 
    // part of the PCA Phase identification process.  So, when the "Use this grid in PCA Phase ID" button is clicked
    // this object will get notified of the user intent.
    public static readonly DependencyProperty HistogramsUsageDelegateProperty = DependencyProperty.Register(
            nameof(HistogramsUsageDelegate),
            typeof(IHistogramsUsageDelegate),
            typeof(ScoresHistogramsView),
            new FrameworkPropertyMetadata(new DoNothingHistogramsUsageDelegate(), HistogramsUsageDelegatePropertyChanged));

    private static void HistogramsUsageDelegatePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
    }

    private static void HistogramsSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScoresHistogramsView scoresHistogramsView) { return; }
        ICollection<IRenderData> renderData = scoresHistogramsView.HistogramsSource;
        scoresHistogramsView.RefreshHistogramData();
    }

    private void HistogramsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshHistogramData();
    }

    // 
    internal string AxisXLabelForHistogramID(string histogramID)
    {
        return "PCA Component " + GridID.IndexForGridLetter(histogramID[0]);
    }


    private void RefreshHistogramData()
    { 
        ICollection<IRenderData> renderDataCollection = this.HistogramsSource;
        int rdc = renderDataCollection.Count;
 
        if (rdc > 0)
        {
            List<IRenderData> renderList = renderDataCollection.ToList();

            if (whichHistogram >= rdc)
            {
                whichHistogram = 0;
            }
            if (whichHistogram < 0)
            {
                whichHistogram = rdc - 1;
            }

            var renderData = renderList[whichHistogram];
            currentHistogramId = renderData.Name;
            List<IRenderData> singleList = new List<IRenderData> { renderData };
            Chart2D histogram = ScoresHistogram;
            //for whatever reason, for grid PQ, we seem to have put the P in the Y axis, and the Q in the Z axis
            // so, the X axis gets its name from the second letter in the grid ID
            histogram.AxisXLabel = AxisXLabelForHistogramID(renderData.Name); 
            histogram.DataSource = singleList;
            histogram.IsLegendVisible = true;
            Label histogramLabel = HistogramLabel;
            histogramLabel.Content = "Histogram " + renderData.Name;

            UseHistogramForPCAPhaseID.IsChecked = HistogramsUsageDelegate.UsesHistogramForPca(currentHistogramId);
            HistogramInfoText.Text = HistogramsUsageDelegate.HistogramInfo(currentHistogramId);
        }
    }

    public IHistogramsUsageDelegate HistogramsUsageDelegate
    {
        get { return (IHistogramsUsageDelegate)GetValue(HistogramsUsageDelegateProperty); }
        set
        {
            SetValue(HistogramsUsageDelegateProperty, value);
        }
    }

    public ICollection<IRenderData> HistogramsSource
    {
        get { return (ICollection<IRenderData>)GetValue(HistogramsSourceProperty); }
        set
        {
            SetValue(HistogramsSourceProperty, value);
            RefreshHistogramData();
        }
    }

    private void AdvanceHistogramButton_Click(object sender, RoutedEventArgs e)
    {
        whichHistogram += 1;
        RefreshHistogramData();
    }
    private void PreviousHistogramButton_Click(object sender, RoutedEventArgs e)
    {
        whichHistogram -= 1;
        RefreshHistogramData();
    }
    private void UseHistogramForPCAPhaseID_Click(object sender, RoutedEventArgs e)
    {
        CheckBox checkBox = (CheckBox)sender;
        bool? checkBoxChecked = checkBox.IsChecked;
        bool isChecked = checkBoxChecked.HasValue ? checkBoxChecked.Value : true;
        HistogramsUsageDelegate.UseHistogramForPca(currentHistogramId, isChecked);
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
