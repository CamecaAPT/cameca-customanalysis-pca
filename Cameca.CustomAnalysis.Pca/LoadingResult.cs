using Cameca.CustomAnalysis.Interface;
using System.Collections.ObjectModel;

namespace Cameca.CustomAnalysis.Pca;

public class LoadingResult
{
    public ObservableCollection<IRenderData> ChartData { get; } = new();
    public string ChartLabel { get; init; }
}