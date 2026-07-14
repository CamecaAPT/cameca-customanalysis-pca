using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Pca.Models;
using Cameca.CustomAnalysis.Pca.Utils;
using Cameca.CustomAnalysis.Pca.VoxelLogic;
using Cameca.CustomAnalysis.PcaLib.Interface;
using Cameca.CustomAnalysis.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using PcaLibPrincipalComponentAnalysis = Cameca.CustomAnalysis.PcaLib.Interface.PrincipalComponentAnalysis;

namespace Cameca.CustomAnalysis.Pca;

[DefaultView(PcaViewModel.UniqueId, typeof(PcaViewModel))]
internal partial class PrincipalComponentAnalysis : BasicCustomAnalysisBase<PcaProperties>
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.PrincipalComponentAnalysis";

    private readonly IOptionsAccessor optionsAccessor;

    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("Principal Component Analysis");

    public ObservableCollection<IRenderData> EigenvalueRenderData { get; } = new();

    [ObservableProperty]
    private ICollection<IRenderData> componentRenderData = Array.Empty<IRenderData>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateCommand))]
    private EigenvalueResults? eigenvalueResults;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateRankEstimationCanExecute))]
    [NotifyCanExecuteChangedFor(nameof(UpdateRankEstimationCommand))]
    private NoiseEigenvalueResults? noiseEigenvalueResults;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateComponentsCanExecute))]
    [NotifyCanExecuteChangedFor(nameof(UpdateComponentsCommand))]
    private ComponentsResults? pcaComponentsResults;

    [ObservableProperty]
    public ICollection<IRenderData> scoresHistogramRenderData = Array.Empty<IRenderData>();

    [ObservableProperty]
    private IColorMap? componentsColorMap = null;

    [ObservableProperty]
    private SeriesCollection loadingsSeries = new();

    [ObservableProperty]
    public ICollection<string> loadingsLabels = Array.Empty<string>();

    [ObservableProperty]
    public double loadingsLabelsRotation = 0d;

    [ObservableProperty]
    public int selectedLoadingsIndex = 0;

    public Func<double, string> AxisYLabelFormatter { get; } = (double value) => value.ToString("F3");

    [ObservableProperty]
    private string loadingsChartTitle = "Loadings";

    [ObservableProperty]
    private ICollection<IRenderData> loadingHistogramRenderData = Array.Empty<IRenderData>();

    private PcaLibPrincipalComponentAnalysis? principalComponentAnalysis = null;
    public PcaLibPrincipalComponentAnalysis? Analysis
    {
        get => principalComponentAnalysis;
        set
        {
            if (!EqualityComparer<PcaLibPrincipalComponentAnalysis>.Default.Equals(principalComponentAnalysis, value))
            {
                principalComponentAnalysis?.Dispose();
                principalComponentAnalysis = value;
            }
        }
    }

    private GridMethod? SelectedGridMethod => Resources.GetValidIonData() is { } ionData
        ? GetVoxelFeatureData(ionData).ExtraData.GridMethod
        : null;

    public bool UpdateRankEstimationCanExecute => NoiseEigenvalueResults is null;

    public bool UpdateComponentsCanExecute => PcaComponentsResults is null;

    public PrincipalComponentAnalysis(
        IStandardAnalysisFilterNodeBaseServices services,
        ResourceFactory resourceFactory,
        IOptionsAccessor optionsAccessor)
        : base(services, resourceFactory)
    {
        this.optionsAccessor = optionsAccessor;
    }

    protected override async Task<bool> Update(CancellationToken cancellationToken)
    {
        await UpdateRankEstimation(cancellationToken);
        return EigenvalueResults is not null;
    }

    protected override byte[]? GetSaveContent()
    {
        if (ComponentsColorMap is not null)
        {
            Properties.ComponentsColorMap = SerializeColorMap(ComponentsColorMap);
        }
        return base.GetSaveContent();
    }

    protected override async IAsyncEnumerable<ReadOnlyMemory<ulong>> GetIndicesDelegateAsync(
        IIonData ownerIonData,
        IProgress<double>? progress,
        [EnumeratorCancellation] CancellationToken token)
    {
        await UpdatePcaResults(Properties.NumberOfComponents, token);
        foreach (var chunk in ownerIonData.AllowAllFilter(progress, token))
        {
            yield return chunk;
        }
    }

    [RelayCommand(CanExecute = nameof(UpdateRankEstimationCanExecute))]
    public async Task UpdateRankEstimation(CancellationToken cancellationToken)
    {
        if (EigenvalueResults is null)
        {
            EigenvalueResults = await GetEigenvalueResults(cancellationToken);
        }
        if ((Analysis ??= await GetAnalysis(cancellationToken)) is { } pca)
        {
            if (SelectedGridMethod != GridMethod.Grid3D)
            {
                int estimatedRank = pca.EstimateRank(
                    Properties.Gaps,
                    (int)Properties.Significance,
                    Properties.Refine);
                var noiseEvals = pca.GetNoiseEigenvalues(estimatedRank);
                NoiseEigenvalueResults = new NoiseEigenvalueResults(estimatedRank, noiseEvals);
            }
            else
            {
                NoiseEigenvalueResults = new NoiseEigenvalueResults(0, Array.Empty<float>());
            }
        }
    }

    // todo: organize
    private async Task UpdatePcaResults(int numberOfComponents, CancellationToken cancellationToken)
    {
        DataStateIsError = false;
        if (await Resources.GetIonData(null, cancellationToken) is not { } ionData
            || ((Analysis ??= await GetAnalysis(cancellationToken)) is not { } pca)
            || pca.Matrix.DataLength == 0
            || GetVoxelFeatureData(ionData) is not { } data)
        {
            DataStateIsError = true;
            return;
        }

        var components = pca.GetComponents(numberOfComponents);

        var voxelIndices = pca.Matrix.GetVoxelIndices();
        // nVoxels x nComponents
        var flatScores = FlatArray(components.Select(x => x.Scores));
        // nFeatures x nComponents
        var flatLoads = FlatArray(components.Select(x => x.Loads));
        int rowsScores = components[0].Scores.Length;  // Safe due to above DataLength check, and assume all are the same size
        int rowsLoads= components[0].Loads.Length;
        int cols = components.Length;

        var packedData = new PackedDataDefinitionBuilder<VoxelFeatureMatrixExtraData>(data.ExtraData)
            .AddPackedData<int>("voxel_indices", voxelIndices, new int[] { voxelIndices.Length })
            .AddPackedData<float>("matrix", flatScores, new int[] { rowsScores, cols }, StorageOrder.F)
            .AddPackedData<float>("loadings", flatLoads, new int[] { rowsLoads, cols }, StorageOrder.F)
            .Build();

        PackedDataSerializer.Write(ionData, Resources.DataSectionName, packedData);

        PcaComponentsResults = new ComponentsResults(pca.GridParams, pca.Matrix.GetVoxelIndices(), components);

        //await DisplayComponentResults(PcaComponentsResults, cancellationToken);
        //await UpdateGrids(cancellationToken);
        SelectedLoadingsIndex = 0;
        UpdateSelected();
    }

    // After setting the number of components, the components data can be computed. We can follow up with the current
    // selected component data as well using the currently selected component index
    [RelayCommand(CanExecute = nameof(UpdateComponentsCanExecute))]
    public async Task UpdateComponents(CancellationToken cancellationToken)
    {
        await UpdatePcaResults(Properties.NumberOfComponents, cancellationToken);
    }

    //public async Task DisplayComponentResults(ComponentsResults componentResults, CancellationToken cancellationToken)
    //{
    //    var pcaPhaseIdProperties = new PcaPhaseIdentificationProperties(Properties.GridProjectionBinSize, Properties.GridProjectionDelocalization, Properties.NoiseFloorFraction, Properties.PeakSummitAllowance, Properties.NumberOfComponents);
    //    ScoresGrid = PcaCalculator.GenerateScoresGrid(compResults, pcaPhaseIdProperties);
    //    PcaTwoDGridsResults = PcaCalculator.CalculateTwoDGrids(ScoresGrid, pcaPhaseIdProperties);
    //    PcaOneDGridsResults = PcaCalculator.CalculateOneDGrids(ScoresGrid, pcaPhaseIdProperties);
    //}

    private async Task<EigenvalueResults?> GetEigenvalueResults(CancellationToken cancellationToken)
    {
        var pca = await GetAnalysis();
        var scores = pca.GetEigenvalues();
        return new EigenvalueResults(scores);
    }

    private async Task<PcaLibPrincipalComponentAnalysis> GetAnalysis(CancellationToken cancellationToken = default)
    {
        if (Analysis is null)
        {
            if (await Resources.GetIonData(cancellationToken: cancellationToken) is not { } ionData)
            {
                throw new InvalidOperationException($"IonData is required to create {nameof(VoxelFeatureMatrix)}");
            }
            Analysis = CreateAnalysis(ionData);
        }
        return Analysis;
    }

    private PcaLibPrincipalComponentAnalysis CreateAnalysis(IIonData ionData)
    {
        if (GetVoxelFeatureData(ionData) is not { } data)
        {
            throw new InvalidOperationException("Parent must define a VoxelFeatureMatrix");
        }

        var gridParams = data.ExtraData.GridParameters;
        var matrix = data.VoxelFeatureMatrix;

        return new PcaLibPrincipalComponentAnalysis(gridParams, matrix);
    }

    private VoxelFeatureMatrixSectionData GetVoxelFeatureData(IIonData ionData)
        => ionData.GetVoxelFeatureMatrixSectionData(Resources.Parent!.DataSectionName);

    // Updates the noise eigenvalues tab plot when the computed eigenvalue data changes
    partial void OnEigenvalueResultsChanged(EigenvalueResults? value)
    {
        EigenvalueRenderData.Clear();
        if (EigenvalueResults is not { Evals: { } evals })
        {
            return;
        }
        var positions = evals.Select((value, index) => new Vector3(index, 0f, value)).ToArray();
        var series = Resources.ChartObjects.CreateSeries(
            positions,
            Colors.Blue,
            markerShape: MarkerShape.Circle,
            name: "Eigenvalues");

        EigenvalueRenderData.Add(series);
    }

    partial void OnNoiseEigenvalueResultsChanged(NoiseEigenvalueResults? value)
    {
        if (NoiseEigenvalueResults is { Rank: int rank, NoiseEvals: float[] noiseEvals })
        {
            if (Properties.NumberOfComponents == 0)
            {
                Properties.NumberOfComponents = rank;
            }
            if (EigenvalueRenderData.FirstOrDefault(x => x.Name == "Noise Eigenvalues") is { } noiseEigenvalues)
            {
                EigenvalueRenderData.Remove(noiseEigenvalues);
            }
            var noisePositions = Enumerable.Range(rank, noiseEvals.Length)
                .Select(index => new Vector3(index, -1f, noiseEvals[index - rank]))
                .ToArray();
            var noiseSeries = Resources.ChartObjects.CreateSeries(
                noisePositions,
                Colors.Red,
                markerShape: MarkerShape.None,
                name: "Noise Eigenvalues");

            EigenvalueRenderData.Add(noiseSeries);
        }
    }

    partial void OnPcaComponentsResultsChanged(ComponentsResults? value)
    {
        ComponentRenderData = Array.Empty<IRenderData>();

        if (PcaComponentsResults is not
            {
                GridParams: { } gridParams,
                Components: { } components,
                VoxelIndices: { } voxelIndices
            }
         || Resources.GetValidIonData() is not { } ionData)
        {
            return;
        }

        int numComponents = Properties.NumberOfComponents;

        var jitterStdDev = optionsAccessor.GetOptions<PcaGlobalOptions>().JitterStdDev;

        var newComponentsData = new IRenderData[numComponents];
        IValuePointsRenderData? rootValuePoints = null;
        for (int compIndex = 0; compIndex < numComponents; compIndex++)
        {
            var componentModel = components[compIndex];
            var scores = componentModel.Scores;
            var positionsWithValues = PositionScores.GetScoredPositions(gridParams, voxelIndices, scores, jitterStdDev: jitterStdDev);

            var valuePoints = Resources.ChartObjects.CreateValuePoints();
            valuePoints.Name = componentModel.Name;
            valuePoints.PositionsWithValues = positionsWithValues;
            if (rootValuePoints is null)
            {
                rootValuePoints = valuePoints;
                rootValuePoints.ColorMap = DeserializeColorMap(Properties.ComponentsColorMap);
            }
            else
            {
                valuePoints.ColorMap = rootValuePoints.ColorMap;
            }

            newComponentsData[compIndex] = valuePoints;
        }

        if (rootValuePoints?.ColorMap is not null)
        {
            ComponentsColorMap = rootValuePoints.ColorMap;
            var range = GetRange(components.Select(x => x.Scores));
            ComponentsColorMap.BottomValue = range.Low;
            ComponentsColorMap.TopValue = range.High;
        }

        ComponentRenderData = newComponentsData;
    }

    protected override void OnDataIsValidChanged(bool isValid)
    {
        if (!isValid)
        {
            EigenvalueResults = null;
            NoiseEigenvalueResults = null;
            Analysis = null;
            PcaComponentsResults = null;
            if (Resources.GetValidIonData() is { } ionData)
            {
                ionData.DeleteSection(Resources.DataSectionName);
            }
            foreach (var child in Resources.Children)
            {
                if (Services.DataStateProvider.Resolve(child.Id) is { } childDataState)
                {
                    childDataState.IsValid = false;
                }
            }
        }
    }

    partial void OnSelectedLoadingsIndexChanged(int value) => UpdateSelected();

    private void UpdateSelected()
    {
        UpdateSelectedLoadings();
        UpdateSelectedScores();
    }

    private void UpdateSelectedLoadings()
    {
        LoadingsLabels = Array.Empty<string>();

        if (Resources.GetValidIonData() is not { } ionData
            || PcaComponentsResults is not { Components: { Length: > 0 } })
        {
            DataStateIsError = true;
            return;
        }

        int numComponents = Properties.NumberOfComponents;
        int selectedIndex = SelectedLoadingsIndex;

        if (PcaComponentsResults.Components.ElementAtOrDefault(selectedIndex) is not { Scores: { } scores, Loads: { } loadingData })
        {
            return;
        }

        // Loading
        int features = loadingData.Length;
        var series = new ColumnSeries
        {
            Name = "",
            Title = "",
            DataLabels = true,
            LabelPoint = x => x.Y.ToString("F3"),
            Values = new ChartValues<float>(loadingData),
        };

        switch (SelectedGridMethod)
        {
            case GridMethod.IonTypes:
            case GridMethod.Grid3D:
                var ions = ionData.Ions;
                var ionBrushes = ions.Select(ionInfo => new SolidColorBrush(Resources.IonDisplayInfo.GetColor(ionInfo))).ToArray();
                var ionMapper = new CartesianMapper<float>()
                    .X((_, i) => i)
                    .Y(value => value)
                    .Fill((_, i) => ionBrushes[i]);
                LoadingsSeries = new SeriesCollection(ionMapper)
                {
                    series
                };
                LoadingsLabels = ions.Select(x => x.Name).ToList();
                LoadingsLabelsRotation = 0d;
                break;
            case GridMethod.Peaks:
                var ionRanges = Resources.RangeManager!.GetIonRanges();
                var peakBrushes = ionRanges.Select(x => new SolidColorBrush(x.Color)).ToArray();
                var peakMapper = new CartesianMapper<float>()
                    .X((_, i) => i)
                    .Y(value => value)
                    .Fill((_, i) => peakBrushes[i]);
                LoadingsSeries = new SeriesCollection(peakMapper)
                {
                    series
                };
                LoadingsLabels = ionRanges
                    .Select(x => $"{x.Name} ({x.Min.ToString("f3")}, {x.Max.ToString("f3")})")
                    .ToList();
                LoadingsLabelsRotation = 45d;
                break;
            case GridMethod.Bins:
                var histogram = Resources.ChartObjects.CreateHistogram(
                    loadingData.Select((y, x) => new Vector2(x, y)).ToArray(),
                    Colors.Black,
                    1f);
                LoadingHistogramRenderData = new IRenderData[] { histogram };
                break;
            default:
                break;
        }
    }

    private void UpdateSelectedScores()
    {
        int selectedIndex = SelectedLoadingsIndex;
        if (selectedIndex < 0
            || Resources.GetValidIonData() is not { } ionData
            || PcaComponentsResults is not { Components: { Length: > 0 } components }
            || selectedIndex >= components.Length)
        {
            DataStateIsError = true;
            return;
        }

        // Scores Histogram
        List<IRenderData> newHistogramsData = new();

        // TODO: don't recreate all the time

        var componentResults = components[selectedIndex];
        float[] scores = componentResults.Scores;
        int voxels = scores.Length;
        float binSize = 0.02f;
        float invBinSize = 1.0f / binSize;
        float min = scores.Min();
        float max = scores.Max();
        int binCount = (int)Math.Ceiling((max - min) / binSize);
        var binnedScores = new int[binCount]; // the y axis of the histogram should be in units of Voxels/PCA Unit
                                                // so that changing the binsize doesn't change the score
        var normalizedScores = new float[binCount];
        for (int i = 0; i < scores.Length; i++)
        {
            int index = (int)((scores[i] - min) / binSize);
            binnedScores[index] += 1;
        }
        var scoreData = binnedScores.Select((y, i) => new Vector2(min + (i * binSize), y * invBinSize)).ToArray();
        var scoresHistogram = Resources.ChartObjects.CreateHistogram(
            scoreData,
            color: Colors.Blue
            //name: GridID.GridLetterForIndex(componentIndex)
            );
        newHistogramsData.Add(scoresHistogram);
        ScoresHistogramRenderData = newHistogramsData;
    }

    private IColorMap DeserializeColorMap(SerializableColorMap? serializedColorMap)
    {
        if (serializedColorMap is not null)
        {
            var colorMap = Resources.ColorMap.CreateColorMap(
                serializedColorMap.Bottom,
                serializedColorMap.NanColor,
                serializedColorMap.OutOfRangeBottom,
                serializedColorMap.OutOfRangeTop,
                serializedColorMap.Top,
                serializedColorMap.ColorStops.Select(x =>
                    Resources.ColorMap.CreateColorStop(x.RelativePosition, x.TopColor, x.BottomColor)));
            colorMap.BottomValue = serializedColorMap.BottomValue;
            colorMap.TopValue = serializedColorMap.TopValue;
            return colorMap;
        }
        else
        {
            var preset = optionsAccessor.GetOptions<PcaGlobalOptions>().ColorMapPreset;
            return Resources.ColorMap.GetPresetColorMap(preset);
        }
    }

    private static SerializableColorMap SerializeColorMap(IColorMap colorMap)
    {
        return new SerializableColorMap
        {
            OutOfRangeTop = colorMap.OutOfRangeTop,
            Top = colorMap.Top,
            NanColor = colorMap.NanColor,
            Bottom = colorMap.Bottom,
            OutOfRangeBottom = colorMap.OutOfRangeBottom,
            ColorStops = colorMap.ColorStops.Select(x => new SerializableColorStop
            {
                TopColor = x.TopColor,
                RelativePosition = x.RelativePosition,
                BottomColor = x.BottomColor,
            }).ToList(),
            BottomValue = colorMap.BottomValue,
            TopValue = colorMap.TopValue,
        };
    }

    private static T[] FlatArray<T>(IEnumerable<T[]> outer)
    {
        var size = outer.Sum(x => x.Length);
        var flattened = new T[size];
        int offset = 0;
        foreach (var x in outer)
        {
            Array.Copy(x, 0, flattened, offset, x.Length);
            offset += x.Length;
        }
        return flattened;
    }

    private static (float Low, float High) GetRange(IEnumerable<float[]> scores)
    {
        // Flatten scores to single array
        var size = scores.Sum(x => x.Length);
        float[] allScores = new float[size];
        int offset = 0;
        foreach (var componentScores in scores)
        {
            int srcSize = componentScores.Length;
            Array.Copy(componentScores, 0, allScores, offset, srcSize);
            offset += srcSize;
        }

        // Get range
        float mean = allScores.Average();
        float stdDev = MathF.Sqrt(allScores.Average(v => MathF.Pow(v - mean, 2)));

        return new(mean - 2 * stdDev, mean + 2 * stdDev);
    }
}
