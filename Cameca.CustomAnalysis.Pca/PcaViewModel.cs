using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Cameca.CustomAnalysis.Pca;

internal class PcaViewModel : AnalysisViewModelBase<PcaNode>
{
    private readonly ResourceFactory resourceFactory;

    public const string UniqueId = "Cameca.CustomAnalysis.Pca.PcaViewModel";

    public PcaNode NodeData => Node;

    public AsyncRelayCommand UpdateCommand { get; }
    public ObservableCollection<IRenderData> NoiseEigenValues { get; } = new();
    public ObservableCollection<LoadingResult> LoadingChartData { get; } = new();

    public PcaViewModel(IAnalysisViewModelBaseServices services, ResourceFactory resourceFactory)
        : base(services)
    {
        this.resourceFactory = resourceFactory;

        UpdateCommand = new AsyncRelayCommand(RunPcaFromGrid3D);
    }

    private async Task RunPcaFromGrid3D()
    {
        int numComponents = NodeData.Options.Components;
        if (Node.Data is null)
        {
            await Node.RunPcaFromGrid3D(numComponents);
            UpdateCommand.NotifyCanExecuteChanged();
        }
        // If still null after recalculate, then no data
        if (Node.Data is not { } results)
        {
            return;
        }

        // Noise Eigenvalues
        NoiseEigenValues.Clear();
        var positions = results.Evals.Select((value, index) => new Vector3(index, 0f, value)).ToArray();
        var chartObjects = resourceFactory.CreateResource(Node.Id).ChartObjects;
        var line = resourceFactory.CreateResource(Node.Id).ChartObjects.CreateLine(positions, Colors.Blue);
        var points = resourceFactory.CreateResource(Node.Id).ChartObjects.CreateSpheres(positions, Colors.Blue, radius: 0.1f, resolution: 20);
        NoiseEigenValues.Add(line);
        NoiseEigenValues.Add(points);

        // Loading
        LoadingChartData.Clear();
        int features = numComponents > 0 ? results.Loads.Length / numComponents : 0;
        for (int i = 0; i < numComponents; i++)
        {
            var loadingData = results.Loads.Skip(i * features).Take(features);

            var histogram = resourceFactory.CreateResource(Node.Id).ChartObjects.CreateHistogram(
                loadingData.Select((x, i) => new Vector2(i, x)).ToArray(),
                color: Colors.Blue);
            var result = new LoadingResult
            {
                ChartLabel = $"Loading {i}",
            };
            result.ChartData.Add(histogram);
            LoadingChartData.Add(result);
        }
    }
}
