using System.Threading;
using System.Windows;

namespace Cameca.CustomAnalysis.Pca;

/// <summary>
/// Interaction logic for PcaView.xaml
/// </summary>
internal partial class PcaView
{
    CancellationTokenSource cancellationTokenSource;
    CancellationToken? incrementIndexCancellationToken = null;
     public PcaView()
    {
        InitializeComponent();
        cancellationTokenSource = new CancellationTokenSource();
    }

    private async void AdvanceComponentButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is PrincipalComponentAnalysis pca)
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
        if (DataContext is PrincipalComponentAnalysis pca)
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