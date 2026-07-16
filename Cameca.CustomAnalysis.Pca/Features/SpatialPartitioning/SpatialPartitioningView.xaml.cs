using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Cameca.CustomAnalysis.Pca;

/// <summary>
/// Interaction logic for SpatialPartitioningView.xaml
/// </summary>
public partial class SpatialPartitioningView : UserControl
{
    CancellationTokenSource cancellationTokenSource;
    CancellationToken? incrementIndexCancellationToken = null;

    public SpatialPartitioningView()
    {
        InitializeComponent();
        cancellationTokenSource = new CancellationTokenSource();
    }

    private async void AdvanceComponentButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SpatialPartitioningAnalysis pca)
        {
            if (incrementIndexCancellationToken == null)
            {
                var cancellationToken = cancellationTokenSource.Token;
                incrementIndexCancellationToken = cancellationToken;
                await pca.IncrementComponentIndex(1, cancellationToken);
                incrementIndexCancellationToken = null;
            }
        }
    }
    private async void PreviousComponentButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SpatialPartitioningAnalysis pca)
        {
            if (incrementIndexCancellationToken == null)
            {
                var cancellationToken = cancellationTokenSource.Token;
                incrementIndexCancellationToken = cancellationToken;
                await pca.IncrementComponentIndex(-1, cancellationToken);
                incrementIndexCancellationToken = null;
            }
        }
    }
}
