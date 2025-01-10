using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using CommunityToolkit.Mvvm.Input;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Cameca.CustomAnalysis.Pca;

internal class PcaViewModel : AnalysisViewModelBase<PcaNode>
{
    private readonly ResourceFactory resourceFactory;

    public const string UniqueId = "Cameca.CustomAnalysis.Pca.PcaViewModel";

    public bool EigenvaluesIsValid => Node.EigenvalueResults is not null;
    public bool ComponentsIsValid => Node.ComponentsResults is not null;

    public AsyncRelayCommand UpdateCommand { get; }

    public ObservableCollection<IRenderData> NoiseEigenValues { get; } = new();

    private SeriesCollection loadingsSeries = new();
    public SeriesCollection LoadingsSeries
    {
        get => loadingsSeries;
        set => SetProperty(ref loadingsSeries, value);
    }

    public ObservableCollection<string> LoadingsLables { get; } = new();
    public ObservableCollection<IRenderData> ScoresHistogramData { get; } = new();

    public PcaViewModel(IAnalysisViewModelBaseServices services, ResourceFactory resourceFactory)
        : base(services)
    {
        this.resourceFactory = resourceFactory;

        UpdateCommand = new AsyncRelayCommand(UpdateAll);
    }

    protected override void OnCreated(ViewModelCreatedEventArgs eventArgs)
    {
        base.OnCreated(eventArgs);
        Node.PropertyChanged += Node_PropertyChanged;
        Node.Options.PropertyChanged += NodeOptions_PropertyChanged;
    }

    protected override void OnAdded(ViewModelAddedEventArgs eventArgs)
    {
        base.OnAdded(eventArgs);
        UpdateNoiseEigenvalue();
        UpdateSelectedComponentCharts();
    }

    protected override void OnClosed(ViewModelDeletedEventArgs eventArgs)
    {
        Node.Options.PropertyChanged -= NodeOptions_PropertyChanged;
        Node.PropertyChanged -= Node_PropertyChanged;
        base.OnClosed(eventArgs);
    }

    private void Node_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Node.EigenvalueResults):
                RaisePropertyChanged(nameof(EigenvaluesIsValid));
                break;
            case nameof(Node.ComponentsResults):
                RaisePropertyChanged(nameof(ComponentsIsValid));
                break;
            default:
                break;
        }
    }

    private void NodeOptions_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Node.Options.ComponentIndex):
                UpdateSelectedComponentCharts();
                break;
            default:
                break;
        }
    }

    private async Task UpdateAll()
    {
        if (!EigenvaluesIsValid || !ComponentsIsValid)
        {
            await Node.RunPcaFromGrid3D();
            UpdateCommand.NotifyCanExecuteChanged();
        }

        UpdateNoiseEigenvalue();
        UpdateSelectedComponentCharts();
    }

    private void UpdateNoiseEigenvalue()
    {
        NoiseEigenValues.Clear();
        if (Node.EigenvalueResults is { Evals: { } evals })
        {
            var resources = resourceFactory.CreateResource(Node.Id);
            var renderDataFactory = resources.ChartObjects;

            var positions = evals.Select((value, index) => new Vector3(index, 0f, value)).ToArray();
            var line = renderDataFactory.CreateLine(positions, Colors.Blue);
            var points = renderDataFactory.CreateSpheres(positions, Colors.Blue, radius: 0.1f, resolution: 20);
            NoiseEigenValues.Add(line);
            NoiseEigenValues.Add(points);
        }
    }

    private void UpdateSelectedComponentCharts()
    {
        LoadingsLables.Clear();
        ScoresHistogramData.Clear();

        var resources = resourceFactory.CreateResource(Node.Id);
        var renderDataFactory = resources.ChartObjects;
        if (resources.GetValidIonData() is not { } ionData)
        {
            throw new InvalidOperationException("Could not resolve ion type information");
        }

        int numComponents = Node.Options.Components;
        int selectedIndex = Node.Options.ComponentIndex;
        if (Node.ComponentsResults?.Components.ElementAtOrDefault(selectedIndex) is not { Scores: { } scores, Loads: { } loadingData })
        {
            return;
        }


        // Loading
        int features = loadingData.Length;
        var ions = ionData.Ions;
        if (ions.Count() != features)
        {
            throw new InvalidOperationException("Count of ion ranges unexpectedly does not match the number of PCA features");
        }

        var series = new ColumnSeries
        {
            Name = "",
            Title = "",
            DataLabels = true,
            LabelPoint = x => x.Y.ToString("F3"),
            Values = new ChartValues<float>(loadingData),
        };

        var ionBrushes = ions.Select(ionInfo => new SolidColorBrush(resources.GetIonColor(ionInfo))).ToArray();
        var mapper = new CartesianMapper<float>()
            .X((_, i) => i)
            .Y(value => value)
            .Fill((_, i) => ionBrushes[i]);
        LoadingsSeries = new SeriesCollection(mapper)
        {
            series
        };
        LoadingsLables.AddRange(ions.Select(x => x.Name));

        // Scores Histogram
        int voxels = scores.Length;
        float binSize = 0.01f;
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
