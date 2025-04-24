using Cameca.CustomAnalysis.Interface;
using Cameca.Extensions.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Cameca.CustomAnalysis.Pca;

/// <summary>
/// Interaction logic for ComponentsTileView.xaml
/// </summary>
public partial class ComponentsTileView : UserControl
{
    public ComponentsTileView()
    {
        InitializeComponent();
        ComponentsItemControl.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
    }

    private void ItemContainerGenerator_StatusChanged(object? sender, EventArgs e)
    {
        if (ComponentsItemControl.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
        {
            ComponentsItemControl.UpdateLayout();
            if (Sync)
            {
                ApplySync();
            }
            else
            {
                ReleaseSync();
            }
        }
    }

    public static readonly DependencyProperty IsPcaPhasesProperty = DependencyProperty.Register(
        nameof(IsPcaPhases), typeof(bool), typeof(ComponentsTileView), new PropertyMetadata(default(bool)));


    public bool? IsPcaPhases
    {
        get
        {  return (bool)GetValue(IsPcaPhasesProperty); }
        set
        {  SetValue(IsPcaPhasesProperty, value); }
    }

    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows), typeof(int), typeof(ComponentsTileView), new PropertyMetadata(default(int)));

    public int Rows
    {
        get { return (int)GetValue(RowsProperty); }
        set { SetValue(RowsProperty, value); }
    }

    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(int), typeof(ComponentsTileView), new PropertyMetadata(default(int)));

    public int Columns
    {
        get { return (int)GetValue(ColumnsProperty); }
        set { SetValue(ColumnsProperty, value); }
    }

    public static readonly DependencyProperty SyncProperty = DependencyProperty.Register(
        nameof(Sync), typeof(bool), typeof(ComponentsTileView), new PropertyMetadata(false, SyncPropertyChanged));

    private static void SyncPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComponentsTileView componentsTileView) { return; }
        if (componentsTileView.Sync)
        {
            componentsTileView.ApplySync();
        }
        else
        {
            componentsTileView.ReleaseSync();
        }
    }

    public bool Sync
    {
        get { return (bool)GetValue(SyncProperty); }
        set { SetValue(SyncProperty, value); }
    }

    public static readonly DependencyProperty ScaleBarVisibleProperty = DependencyProperty.Register(
        nameof(ScaleBarVisible), typeof(bool), typeof(ComponentsTileView), new PropertyMetadata(false));


    public bool ScaleBarVisible
    {
        get { return (bool)GetValue(ScaleBarVisibleProperty); }
        set { SetValue(ScaleBarVisibleProperty, value); }
    }


    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(ICollection<IRenderData>),
        typeof(ComponentsTileView),
        new FrameworkPropertyMetadata(Array.Empty<IRenderData>(), ItemsSourcePropertyChanged));

    private static void ItemsSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComponentsTileView componentsTileView) { return; }

        int componentCount = componentsTileView.ItemsSource.Count;
        double root = Math.Sqrt(componentCount);
        componentsTileView.Columns = (int)Math.Ceiling(root);
        componentsTileView.Rows = componentsTileView.Columns > 0 ? (componentCount / componentsTileView.Columns) + (componentCount % componentsTileView.Columns > 0 ? 1 : 0) : 0;


        //if (e.OldValue is INotifyCollectionChanged oldValue)
        //{
        //    oldValue.CollectionChanged -= componentsTileView.ItemsSourceCollectionChanged;
        //}
        //if (e.NewValue is INotifyCollectionChanged newValue)
        //{
        //    newValue.CollectionChanged += componentsTileView.ItemsSourceCollectionChanged;
        //}
    }

    private void ItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is ICollection enumerable)
        {
            int componentCount = enumerable.Count;
            double root = Math.Sqrt(componentCount);
            Columns = (int)Math.Ceiling(root);
            Rows = Columns > 0 ? (componentCount / Columns) + (componentCount % Columns > 0 ? 1 : 0) : 0;

            if (Sync)
            {
                ApplySync();
            }
            else
            {
                ReleaseSync();
            }
        }
    }

    public ICollection<IRenderData> ItemsSource
    {
        get { return (ICollection<IRenderData>)GetValue(ItemsSourceProperty); }
        set { SetValue(ItemsSourceProperty, value); }
    }

    private void ApplySync()
    {
        var chartControls = GetChildren<Chart3D>(ComponentsItemControl).ToList();
        if (chartControls.Count > 0)
        {
            chartControls[0].Sync(null);
        }
        if (chartControls.Count > 1)
        {
            var last = chartControls[0];
            var current = last;
            for (int i = 1; i < chartControls.Count; i++)
            {
                current = chartControls[i];
                current.Sync(last);
                last = current;
            }
            chartControls[0].Sync(last);
        }
    }

    private void ReleaseSync()
    {
        var chartControls = GetChildren<Chart3D>(ComponentsItemControl).ToList();
        if (chartControls.Count > 0)
        {
            chartControls[0].ReleaseSync(null);
        }
        if (chartControls.Count > 1)
        {
            var last = chartControls[0];
            var current = last;
            for (int i = 1; i < chartControls.Count; i++)
            {
                current = chartControls[i];
                current.ReleaseSync(last);
                last = current;
            }
            chartControls[0].ReleaseSync(last);
        }
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
