using System.Windows;

namespace Cameca.CustomAnalysis.Pca;

/// <summary>
/// Interaction logic for PcaView.xaml
/// </summary>
internal partial class PcaView
{

    public PcaView()
    {
        InitializeComponent();
    }

    private void AdvanceComponentButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is PrincipalComponentAnalysis pca)
        {
            pca.IncrementComponentIndex(1);
        }
    }
    private void PreviousComponentButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is PrincipalComponentAnalysis pca)
        {
            pca.IncrementComponentIndex(-1);
        }
    }
}