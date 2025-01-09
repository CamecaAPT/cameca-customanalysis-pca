using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Cameca.CustomAnalysis.Pca;

internal class PcaViewModel : AnalysisViewModelBase<PcaNode>
{
    private readonly ResourceFactory resourceFactory;

    public const string UniqueId = "Cameca.CustomAnalysis.Pca.PcaViewModel";

    public PcaNode NodeData => Node;

    public AsyncRelayCommand UpdateCommand { get; }
    public ObservableCollection<IRenderData> NoiseEigenValues { get; } = new();
    public ObservableCollection<IRenderData> LoadingsHistogramData { get; } = new();
    public ObservableCollection<IRenderData> ScoresHistogramData { get; } = new();

    public PcaViewModel(IAnalysisViewModelBaseServices services, ResourceFactory resourceFactory)
        : base(services)
    {
        this.resourceFactory = resourceFactory;

        UpdateCommand = new AsyncRelayCommand(RunPcaFromGrid3D);
    }

    private async Task RunPcaFromGrid3D()
    {
        int numComponents = NodeData.Options.Components;
        int selectedIndex = NodeData.Options.ComponentIndex;
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

        var renderDataFactory = resourceFactory.CreateResource(Node.Id).ChartObjects;

        // Noise Eigenvalues
        NoiseEigenValues.Clear();
        var positions = results.Evals.Select((value, index) => new Vector3(index, 0f, value)).ToArray();
        var line = renderDataFactory.CreateLine(positions, Colors.Blue);
        var points = renderDataFactory.CreateSpheres(positions, Colors.Blue, radius: 0.1f, resolution: 20);
        NoiseEigenValues.Add(line);
        NoiseEigenValues.Add(points);

        // Loading
        LoadingsHistogramData.Clear();
        int features = numComponents > 0 ? results.Loads.Length / numComponents : 0;
        var selectionLoadsSlice = results.Loads.Skip(selectedIndex * features).Take(features);
        var loadingData = selectionLoadsSlice.Select((x, i) => new Vector2(i, x)).ToArray();
        var histogram = renderDataFactory.CreateHistogram(
            loadingData,
            color: Colors.Blue);
        LoadingsHistogramData.Add(histogram);

        // Scores Histogram
        int voxels = numComponents > 0 ? results.Scores.Length / numComponents : 0;
        ScoresHistogramData.Clear();
        float binSize = 0.01f;
        var scores = results.Scores.Skip(selectedIndex * voxels).Take(voxels).ToArray();
        float min = scores.Min();
        float max = scores.Max();
        int binCount = (int)Math.Ceiling((max - min) / binSize);
        var binnedScores = new int[binCount];
        for (int i = 0; i < scores.Length; i++)
        {
            int index = (int)((scores[i] - min) / binSize);
            binnedScores[index]++;
        }
        var scoreData = binnedScores.Select((y, i) => new Vector2(min + (i * binSize), y)).ToArray();
        var scoresHistogram = renderDataFactory.CreateHistogram(
            scoreData,
            color: Colors.Blue);
        ScoresHistogramData.Add(scoresHistogram);
    }
}
