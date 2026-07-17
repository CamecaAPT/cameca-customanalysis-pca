using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using CommunityToolkit.HighPerformance.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using System.Collections.ObjectModel;
using CommunityToolkit.HighPerformance;
using Cameca.CustomAnalysis.Pca;
using Cameca.CustomAnalysis.Pca.Utils;
using Cameca.CustomAnalysis.Pca.Models;
using Cameca.CustomAnalysis.Pca.VoxelLogic;
using Cameca.CustomAnalysis.PcaLib.Interface;
using PcaLibPrincipalComponentAnalysis = Cameca.CustomAnalysis.PcaLib.Interface.PrincipalComponentAnalysis;
using Cameca.CustomAnalysis.Utilities.Segmentation;
using System.Text.Json;

namespace Cameca.CustomAnalysis.Pca;

[DefaultView(SpatialPartitioningViewModel.UniqueId, typeof(SpatialPartitioningViewModel))]
internal partial class SpatialPartitioningAnalysis : BasicCustomAnalysisBase<SpatialPartitioningProperties>, IGridsUsageDelegate, IHistogramsUsageDelegate
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.SpatialPartitioningAnalysis";

    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("Spatial Partitioning");

    private readonly INodeDataProvider nodeDataProvider;
    private readonly IOptionsAccessor optionsAccessor;
    private readonly SegmentedRoiManager<IStandardAnalysisFilterNodeBaseServices> segmentedManager;

    [ObservableProperty]
    private ICollection<IRenderData> componentRenderData = Array.Empty<IRenderData>();

    [ObservableProperty]
    private ICollection<IRenderData> pcaPhasesRenderData = Array.Empty<IRenderData>();

    [ObservableProperty]
    private IColorMap? pcaColorMap = null;

    [ObservableProperty]
    public ICollection<IRenderData> scoresHistogramRenderData = Array.Empty<IRenderData>();

    [ObservableProperty]
    private ICollection<IRenderData> selectedGridRenderDataTinted = Array.Empty<IRenderData>();
    [ObservableProperty]
    private ICollection<IRenderData> selectedGridRenderDataUntinted = Array.Empty<IRenderData>();

    [ObservableProperty]
    private IGridsUsageDelegate gridsUsageDelegate = new DoNothingGridsUsageDelegate();

    [ObservableProperty]
    private IGridsColorMapProvider gridsColorMapProvider = new PcaGridsColorMapProvider(null);

    [ObservableProperty]
    private IHistogramsUsageDelegate histogramsUsageDelegate = new DoNothingHistogramsUsageDelegate();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateComponentsCanExecute))]
    private ComponentsResults? pcaComponentsResults;

    [ObservableProperty]
    private PcaScoresGrid? scoresGrid = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateGridsCanExecute))]
    [NotifyCanExecuteChangedFor(nameof(UpdateGridsCommand))]
    private OneDGridsResults? pcaOneDGridsResults;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateGridsCanExecute))]
    [NotifyCanExecuteChangedFor(nameof(UpdateGridsCommand))]
    private TwoDGridsResults? pcaTwoDGridsResults;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdatePCAPhasesCanExecute))]
    [NotifyCanExecuteChangedFor(nameof(UpdatePCAPhasesCommand))]
    private PhaseIdResults? pcaPhaseIDResults;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateSelectedComponentCanExecute))]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedComponentCommand))]
    private SeriesCollection loadingsSeries = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateSelectedComponentCanExecute))]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedComponentCommand))]
    public ICollection<string> loadingsLabels = Array.Empty<string>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateSelectedComponentCanExecute))]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedComponentCommand))]
    public double loadingsLabelsRotation = 0d;

    public bool UpdateComponentsCanExecute => PcaComponentsResults is null;
    public bool UpdateGridsCanExecute => PcaTwoDGridsResults is null;
    public bool UpdatePCAPhasesCanExecute => PcaPhaseIDResults is null;

    private VoxelFeatureMatrixSectionData? data = null;

    public bool UpdateSelectedComponentCanExecute
    {
        get
        {
            return data?.ExtraData.GridMethod == GridMethod.Bins
                ? !LoadingHistogramRenderData.Any()
                : !LoadingsSeries.Any() || !LoadingsLabels.Any() || !ScoresHistogramRenderData.Any();
        }
    }

    public Func<double, string> AxisYLabelFormatter { get; } = (double value) => value.ToString("F3");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateSelectedComponentCanExecute))]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedComponentCommand))]
    private string loadingsChartTitle = "Loadings";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateSelectedComponentCanExecute))]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedComponentCommand))]
    private ICollection<IRenderData> loadingHistogramRenderData = Array.Empty<IRenderData>();

    internal HashSet<string> gridsToUseForPCA = new();
    internal HashSet<string> histogramsToUseForPCA = new();

    private int? numberOfComponents => data?.VoxelFeatureMatrix.FeatureCount;

    public SpatialPartitioningAnalysis(
        IStandardAnalysisFilterNodeBaseServices services,
        ResourceFactory resourceFactory,
        INodeDataProvider nodeDataProvider,
        IOptionsAccessor optionsAccessor)
        : base(services, resourceFactory)
    {
        this.nodeDataProvider = nodeDataProvider;
        this.optionsAccessor = optionsAccessor;
        this.gridsUsageDelegate = this;
        this.gridsColorMapProvider = new PcaGridsColorMapProvider(null);
        this.histogramsUsageDelegate = this;
        segmentedManager = SegmentedRoiManager.Create(this);
    }

    public bool UsesGridForPca(string gridID)
    {
        return gridsToUseForPCA.Contains(gridID);
    }

    public void UseGridForPca(string gridID, bool useIt)
    {
        if (useIt)
        {
            gridsToUseForPCA.Add(gridID);
        }
        else
        {
            gridsToUseForPCA.Remove(gridID);
        }
        InvalidatePcaPhases();
    }

    public string GridInfo(string gridID)
    {
        string info = "";
        if (scoresGrid != null)
        {
            info = scoresGrid.GridInfo(gridID);
        }
        return info;
    }

    public bool UsesHistogramForPca(string gridID)
    {
        return histogramsToUseForPCA.Contains(gridID);
    }

    public void UseHistogramForPca(string gridID, bool useIt)
    {
        if (useIt)
        {
            histogramsToUseForPCA.Add(gridID);
        }
        else
        {
            histogramsToUseForPCA.Remove(gridID);
        }
        InvalidatePcaPhases();
    }

    public string HistogramInfo(string histogramID)
    {
        string info = "";
        if (scoresGrid != null)
        {
            info = scoresGrid.HistogramInfo(histogramID);
        }
        return info;
    }
    protected override void OnAdded(NodeAddedEventArgs eventArgs)
    {
        base.OnAdded(eventArgs);
        this.gridsColorMapProvider.SetColorMapFactory(Resources.ColorMap);
        gridsToUseForPCA = Properties.GridsToUseForPCA.ToHashSet();
        histogramsToUseForPCA = Properties.HistogramsToUseForPCA.ToHashSet();
    }

    protected override byte[]? GetSaveContent()
    {
        if (PcaColorMap is not null)
        {
            Properties.PcaColorMap = SerializeColorMap(PcaColorMap);
        }
        Properties.GridsToUseForPCA = gridsToUseForPCA.ToList();
        Properties.HistogramsToUseForPCA = histogramsToUseForPCA.ToList();
        return base.GetSaveContent();
    }

    protected override void OnDataIsValidChanged(bool isValid)
    {
        // If the underlying data was changed (e.g. ranged changed or analysis is in an ROI that was altered), then all data needs to be recomputed
        if (!isValid)
        {
            InvalidateAll();
        }
    }

    // Update from full raw data: Sets Eigenvalues only - currently noise eigenvalues must be manually analyzed to set the appropriate number of components after full data change.
    // There is not benefit of prematurely calculating all the other data until the number of components is manually set after looking at the scree plot
    protected override async Task<bool> Update(CancellationToken cancellationToken)
    {
        if ((await Resources.GetIonData(cancellationToken: cancellationToken)) is not { } ionData)
        {
            return false;
        }
        data = ionData.GetVoxelFeatureMatrixSectionData(Resources.Parent!.DataSectionName);
        if (data is null || data.VoxelFeatureMatrix.DataLength == 0)
        {
            return false;
        }

        //var components = Analysis.GetComponents(Properties.NumberOfComponents);
        var scores = data.VoxelFeatureMatrix;
        var loadings = data.LoadingsMatrix!;
        if (scores.FeatureCount != loadings.FeatureCount)
        {
            throw new InvalidOperationException("scores and loadings have mismatched dimensions");
        }
        int count = scores.FeatureCount;
        ComponentData[] components = new ComponentData[count];
        for (int i = 0; i < count; ++i)
        {
            components[i] = new ComponentData(
                scores.GetFeatureData(i).ToArray(),
                loadings.GetFeatureData(i).ToArray());
        }

        PcaComponentsResults = new ComponentsResults(
            data.ExtraData.GridParameters,
            data.VoxelFeatureMatrix.GetVoxelIndices(),
            components);

        UpdateOptionsBounds();

        // Ensure that the selected component falls in the valid range of number of components
        // operate on a local variable and update only on a change so that we dont
        // trigger the ObservedPropertyChanges callbacks
        var inRangeIndex = Properties.ComponentIndex;
        if (inRangeIndex >= count)
        {
            inRangeIndex = count - 1;
        }
        if (inRangeIndex < 0)
        {
            inRangeIndex = 0;
        }
        if (inRangeIndex != Properties.ComponentIndex)
        {
            Properties.ComponentIndex = inRangeIndex;
        }

        inRangeIndex = Properties.PcaPhaseIndex;
        if (inRangeIndex >= count)
        {
            inRangeIndex = count - 1;
        }
        if (inRangeIndex < 0)
        {
            inRangeIndex = 0;
        }
        if (inRangeIndex != Properties.PcaPhaseIndex)
        {
            Properties.PcaPhaseIndex = inRangeIndex;
        }

        await UpdateHistograms(cancellationToken);
        await UpdateGrids(cancellationToken);
        await UpdateSelectedComponent(cancellationToken);
        return true;
    }

    [RelayCommand(CanExecute = nameof(UpdateGridsCanExecute))]
    public async Task UpdateHistograms(CancellationToken cancellationToken)
    {
        if (PcaComponentsResults is null)
        {
            await Update(cancellationToken);
        }

        var componentsResults = PcaComponentsResults;
        if (componentsResults is null)
        {
            return;
        }
        // Scores Histogram
        List<IRenderData> newHistogramsData = new();
        int componentIndex = 0;

        foreach (var componentResults in componentsResults.Components)
        {
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
                color: Colors.Blue,
                name: GridID.GridLetterForIndex(componentIndex));
            newHistogramsData.Add(scoresHistogram);
            ++componentIndex;
        }
        ScoresHistogramRenderData = newHistogramsData;
    }

    // PCA Phases can be updated independently of the Components if the number of grids to use changes
    // or if any of the properties to use in the calculation change
    [RelayCommand(CanExecute = nameof(UpdateGridsCanExecute))]
    public async Task UpdateGrids(CancellationToken cancellationToken)
    {
        if (PcaComponentsResults is null)
        {
            await Update(cancellationToken);
        }

        var compResults = PcaComponentsResults;
        if (compResults != null && data?.VoxelFeatureMatrix.FeatureCount is { } componentCount)
        {
            var pcaPhaseIdProperties = new PcaPhaseIdentificationProperties(
                Properties.GridProjectionBinSize,
                Properties.GridProjectionDelocalization,
                Properties.NoiseFloorFraction,
                Properties.PeakSummitAllowance,
                componentCount);
            ScoresGrid = PcaCalculator.GenerateScoresGrid(compResults, pcaPhaseIdProperties);
            PcaTwoDGridsResults = PcaCalculator.CalculateTwoDGrids(ScoresGrid, pcaPhaseIdProperties);
            PcaOneDGridsResults = PcaCalculator.CalculateOneDGrids(ScoresGrid, pcaPhaseIdProperties);
        }
    }

    // Uses the component data (or computes for all components if necessary) to generate plots for the selected component by index
    [RelayCommand(CanExecute = nameof(UpdateSelectedComponentCanExecute))]
    public async Task UpdateSelectedComponent(CancellationToken cancellationToken)
    {
        LoadingsLabels = Array.Empty<string>();

        if (await Resources.GetIonData(cancellationToken: cancellationToken) is not { } ionData || data is null)
        {
            DataStateIsError = true;
            return;
        }

        int numComponents = data.VoxelFeatureMatrix.FeatureCount;
        int selectedIndex = Properties.ComponentIndex;

        if ((PcaComponentsResults is null) || (PcaComponentsResults.Components.Count() == 0))
        {
            await Update(cancellationToken);
        }

        if (PcaComponentsResults?.Components.ElementAtOrDefault(selectedIndex) is not { Scores: { } scores, Loads: { } loadingData })
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

        switch (data.ExtraData.GridMethod)
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

    // PCA Phases can be updated independently of the Components and grids   if the number of grids to use changes
    // or if any of the properties to use in the calculation change
    [RelayCommand(CanExecute = nameof(UpdatePCAPhasesCanExecute))]
    public void UpdatePCAPhases()
    {
        var scoresGrid = ScoresGrid;
        var oneDGridsResults = PcaOneDGridsResults;
        var twoDGridsResults = PcaTwoDGridsResults;

        if ((scoresGrid != null) && (oneDGridsResults != null) && (twoDGridsResults != null) && (data?.VoxelFeatureMatrix.FeatureCount is { } componentCount))
        {
            var pcaPhaseIdProperties = new PcaPhaseIdentificationProperties(
                Properties.GridProjectionBinSize,
                Properties.GridProjectionDelocalization,
                Properties.NoiseFloorFraction,
                Properties.PeakSummitAllowance,
                componentCount);
            var results = scoresGrid.GetPhasesStrategyF(pcaPhaseIdProperties, gridsToUseForPCA, histogramsToUseForPCA);
            SetPcaPhaseIDResults(results);
        }
    }

    // Updates the components 3D plots when the component data (derived from selected number of components) changes
    static float[] GetPhaseIdScoresForVoxelIndices(PhaseIdResults phaseIdResults, int compIndex, int[] voxelIndices)
    {
        int numIndices = voxelIndices.Length;
        float[] scores = new float[numIndices];
        for (int i = 0; i < numIndices; ++i)
        {
            int voxelIndex = voxelIndices[i];
            scores[i] = phaseIdResults.PhaseForVoxelIntValue(voxelIndex) == compIndex ? 1.0f : 0.0f;
        }
        return scores;
    }

    // Updates the components 3D plots when the component data (derived from selected number of components) changes
    partial void OnPcaPhaseIDResultsChanged(PhaseIdResults? value)
    {
        if (PcaPhaseIDResults is
            {
                IdentifiedPhase: Dictionary<VoxelID, int> identifiedPhase,
                PhaseIndexMap: Dictionary<PcaPhaseName, int> phaseIndexMap
            })

        {
            PcaPhasesRenderData = Array.Empty<IRenderData>();
            if (PcaComponentsResults is null)
            {
                return;
            }
            var voxelIndices = PcaComponentsResults.VoxelIndices;
            if (voxelIndices is null)
            {
                return;
            }
            var jitterStdDev = optionsAccessor.GetOptions<PcaGlobalOptions>().JitterStdDev;

            Dictionary<int, PcaPhaseName> phaseNamesMap = PcaPhaseIDResults.PhaseNamesMap();
            int numPhases = phaseNamesMap.Count;
            // int selectedIndex = Properties.ComponentIndex;

            var newPhasesData = new IRenderData[numPhases];
            IValuePointsRenderData? rootValuePoints2 = null;
            for (int compIndex = 0; compIndex < numPhases; compIndex++)
            {
                var phaseIdScores = GetPhaseIdScoresForVoxelIndices(PcaPhaseIDResults, compIndex, voxelIndices);

                // data fed into GetScoredPositions is an array of voxelIndices for which a dot should be generated,
                // and an array of scores -- scores[n] is the score for the voxel at voxelIndex[n]
                var positionsWithValues = PositionScores.GetScoredPositions(PcaComponentsResults.GridParams, voxelIndices, phaseIdScores, jitterStdDev: jitterStdDev);

                var valuePoints = Resources.ChartObjects.CreateValuePoints();

                valuePoints.Name = phaseNamesMap[compIndex].UserDisplayableName();
                valuePoints.PositionsWithValues = positionsWithValues;
                if (rootValuePoints2 is null)
                {
                    rootValuePoints2 = valuePoints;
                    rootValuePoints2.ColorMap = DeserializeColorMap(Properties.PcaColorMap);
                }
                else
                {
                    valuePoints.ColorMap = rootValuePoints2.ColorMap;
                }

                newPhasesData[compIndex] = valuePoints;
            }

            if (rootValuePoints2?.ColorMap is not null)
            {
                PcaColorMap = rootValuePoints2.ColorMap;
                PcaColorMap.BottomValue = 0.0f;
                PcaColorMap.TopValue = 1.0f;
            }

            PcaPhasesRenderData = newPhasesData;

        }
    }
    partial void OnPcaOneDGridsResultsChanged(OneDGridsResults? value)
    {
        if (PcaOneDGridsResults is
            {
                OneDPeakProjections: Dictionary<OneDGridID, OneDPeakProjection> oneDPeakProjections
            })
        {
            // Scores Histogram
            List<IRenderData> newHistogramsData = new List<IRenderData>();
            int componentIndex = 0;
            foreach (KeyValuePair<OneDGridID, OneDPeakProjection> kvp in oneDPeakProjections)
            {
                OneDGridID gridId = kvp.Key;
                DensityLine line = kvp.Value.densityLine;
                BinID minBin;
                BinID maxBin;

                (minBin, maxBin) = line.MinMaxBinIDs();
                float binSize = line.binsize;
                float invBinSize = 1.0f / binSize;
                int min = minBin.XCoord();
                int max = maxBin.XCoord();
                int binCount = (1 + max - min);
                var binnedScores = new float[binCount]; // the y axis of the histogram should be in units of Voxels/PCA Unit
                                                        // so that changing the binsize doesn't change the score
                var normalizedScores = new float[binCount];
                BinID bin = minBin;
                for (int i = 0; i < binCount; i++)
                {
                    binnedScores[i] = line.ValueAtBin(bin);
                    bin = bin.NextHigherBin();
                }

                // when defining scoreData, multiply the y by invBinSize to get the units right
                //  subtract halfBinsize from the x to get the rendering of the x coordinate right, because LiveCharts
                //  doesn't draw the bin at the center, but instead draws it at the low side of the bin 
                float halfBinsize = binSize * 0.5f;
                var scoreData = binnedScores.Select((y, i) => new Vector2((min + i) * binSize - halfBinsize, y * invBinSize)).ToArray();
                var scoresHistogram = Resources.ChartObjects.CreateHistogram(scoreData, color: Colors.Blue);
                scoresHistogram.Name = gridId.ToString();
                newHistogramsData.Add(scoresHistogram);
                ++componentIndex;
            }
            ScoresHistogramRenderData = newHistogramsData;
        }
    }
    partial void OnPcaTwoDGridsResultsChanged(TwoDGridsResults? value)
    {
        if (PcaTwoDGridsResults is
            {
                TwoDPeakProjections: Dictionary<TwoDGridID, TwoDPeakProjection> twoDPeakProjections
            })
        {
            SelectedGridRenderDataTinted = Array.Empty<IRenderData>();
            SelectedGridRenderDataUntinted = Array.Empty<IRenderData>();
            int gridCount = twoDPeakProjections.Count;
            var newGridProjectionsDataTinted = new List<IRenderData>();
            var newGridProjectionsDataUntinted = new List<IRenderData>();
            foreach (KeyValuePair<TwoDGridID, TwoDPeakProjection> kvp in twoDPeakProjections)
            {
                var histogramTinted = Resources.ChartObjects.CreateHistogram2D();
                var histogramUntinted = Resources.ChartObjects.CreateHistogram2D();
                histogramTinted.Name = kvp.Key.ToString();
                histogramTinted.ColorMap = GridsColorMapProvider.ColorMapForGrid(true);
                histogramUntinted.Name = kvp.Key.ToString();
                histogramUntinted.ColorMap = GridsColorMapProvider.ColorMapForGrid(false);
                FillRenderDataWithGridData(histogramTinted, kvp.Value);
                FillRenderDataWithGridData(histogramUntinted, kvp.Value);
                newGridProjectionsDataTinted.Add(histogramTinted);
                newGridProjectionsDataUntinted.Add(histogramUntinted);
            }
            SelectedGridRenderDataTinted = newGridProjectionsDataTinted;
            SelectedGridRenderDataUntinted = newGridProjectionsDataUntinted;
        }
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

    private IColorMap GetColorMap(IValuePointsRenderData? rootValuePoints)
    {
        if (rootValuePoints?.ColorMap is not null)
        {
            return rootValuePoints.ColorMap;
        }
        return Resources.ColorMap.CreateColorMap();
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
    static private SerializableColorMap SerializeColorMap(IColorMap colorMap)
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

    static public void FillRenderDataWithGridData(IHistogram2DRenderData renderData, TwoDPeakProjection projection)
    {
        // FillRenderDataWithGridDataGrayscale(renderData, projection);
        FillRenderDataWithGridDataPeaksHighlighted(renderData, projection);
    }

    static public void FillRenderDataWithGridDataPeaksHighlighted(IHistogram2DRenderData renderData, TwoDPeakProjection projection)
    {
        // for the peaks highlighted variant
        // add 3, 2 or 1 to the value in the pixel to get a highlight for peak 1 2 or three
        // it is reverse so that the grid will be sure to have a pixel near the maximum
        // peak 4 can reuse peak 1, etc.
        DensityPlane dp = projection.densityPlane;
        (TwoDGridCoord minCoord, TwoDGridCoord maxCoord) = dp.MinMaxGridCoords();
        int spanX = 1 + maxCoord.x - minCoord.x;
        int spanY = 1 + maxCoord.y - minCoord.y;
        int span = Math.Max(spanX, spanY);
        int numCoords = span * span;
        float[] dat = new float[numCoords * 4];

        // first, need to find the maximum value so we can normalize everything here
        var maxGridValue = dp.GridMaximumValue();
        for (int q = 0; q < spanY; q += 1)
        {
            int qOffset = q * span;
            int gridq = q + minCoord.y;
            for (int p = 0; p < spanX; p += 1)
            {
                int arrayIndex = qOffset + p;
                int gridp = p + minCoord.x;
                int partitionIndex = dp.PartitionIndexAtGridCoords(gridp, gridq);
                int peakTintAddition = partitionIndex == 0 ? 0 : ((partitionIndex + 1) % 3) + 1;
                float fractionOfMax = dp.ValueAtGridCoords(gridp, gridq) / maxGridValue;
                dat[arrayIndex] = peakTintAddition + fractionOfMax;
            }
        }
        ReadOnlyMemory2D<float> rom = new(dat, span, span);
        Vector2 binsize = new(dp.binsize, dp.binsize);
        // LiveCharts doesn't draw the bin at the bin center, but rather at the low coordinate of the bin
        //  so, we need to subtract half the binsize from the x y coordinates to get it to render correctly
        float halfBinsize = dp.binsize * 0.5f;
        Vector2 origin = new((minCoord.y * dp.binsize) - halfBinsize, (minCoord.x * dp.binsize) - halfBinsize);
        renderData.Update(rom, binsize, origin);
    }

    static public void FillRenderDataWithGridDataGrayscale(IHistogram2DRenderData renderData, TwoDPeakProjection projection)
    {
        // renderData.ColorMap = Resources.ColorMap.GetPresetColorMap(ColorMapPreset.GreyScale);
        DensityPlane dp = projection.densityPlane;
        (TwoDGridCoord minCoord, TwoDGridCoord maxCoord) = dp.MinMaxGridCoords();
        int spanX = 1 + maxCoord.x - minCoord.x;
        int spanY = 1 + maxCoord.y - minCoord.y;
        int span = Math.Max(spanX, spanY);
        int numCoords = span * span;
        float[] dat = new float[numCoords * 4];
        for (int q = 0; q < spanY; q += 1)
        {
            int qOffset = q * span;
            int gridq = q + minCoord.y;
            for (int p = 0; p < spanX; p += 1)
            {
                int arrayIndex = qOffset + p;
                int gridp = p + minCoord.x;
                dat[arrayIndex] = dp.ValueAtGridCoords(gridp, gridq);
            }
        }
        ReadOnlyMemory2D<float> rom = new(dat, span, span);
        Vector2 binsize = new(dp.binsize, dp.binsize);
        // LiveCharts doesn't draw the bin at the bin center, but rather at the low coordinate of the bin
        //  so, we need to subtract half the binsize from the x y coordinates to get it to render correctly
        float halfBinsize = dp.binsize * 0.5f;
        Vector2 origin = new((minCoord.y * dp.binsize) - halfBinsize, (minCoord.x * dp.binsize) - halfBinsize);
        renderData.Update(rom, binsize, origin);
    }

    // Updates readonly Min/Max properties so the bounds are displayed in the Properties panel 
    private void UpdateOptionsBounds()
    {
        var selectedResult = PcaComponentsResults?.Components.ElementAtOrDefault(Properties.ComponentIndex);
        if (selectedResult != null)
        {
            if (selectedResult.Scores != null)
            {
                Properties.Min = selectedResult.Scores.Min();
                Properties.Max = selectedResult.Scores.Max();
            }
        }
    }

    // Applies filter to the custom analysis: returns the ions in voxels for which the score of the selected component exceeds the specified threshold value
    // Olof Note -- this is where to change logic for viewing results -- redirect the algorithm here to look at the new 'identified phase' structure

    protected override async IAsyncEnumerable<ReadOnlyMemory<ulong>> GetIndicesDelegateAsync(
        IIonData ownerIonData,
        IProgress<double>? progress,
        [EnumeratorCancellation] CancellationToken token)
    {
        await Update(token);
        foreach (var chunk in ownerIonData.AllowAllFilter(progress, token))
        {
            yield return chunk;
        }
    }

    // On Properties panel changes, some data must be invalidated to be recomputed with new values. Invalidations depend on the properties changed
    protected override void OnPropertiesChanged(PropertyChangedEventArgs e)
    {
        // Base call invalidates the returned filter - this correctly needs to be invalidated for any of the current editable properties
        base.OnPropertiesChanged(e);
        CanSave = true;
        switch (e.PropertyName)
        {
            case nameof(SpatialPartitioningProperties.ComponentIndex):
                UpdateOptionsBounds();
                InvalidateSelectedComponent();
                break;
            case nameof(SpatialPartitioningProperties.Invert):
                FilterIsInverted = Properties.Invert;
                InvalidateSelectedComponent();
                break;
            case nameof(SpatialPartitioningProperties.GridProjectionBinSize):
                InvalidatePcaGrids();
                break;
            case nameof(SpatialPartitioningProperties.GridProjectionDelocalization):
                InvalidatePcaGrids();
                break;
            case nameof(SpatialPartitioningProperties.NoiseFloorFraction):
                InvalidatePcaGrids();
                break;
            case nameof(SpatialPartitioningProperties.PeakSummitAllowance):
                InvalidatePcaGrids();
                break;
            default:
                break;
        }
    }

    /* Data invalidation methods */
    private void InvalidateSelectedComponent()
    {
        LoadingHistogramRenderData = Array.Empty<IRenderData>();
        LoadingsSeries = new SeriesCollection();
        LoadingsLabels = Array.Empty<string>();
        LoadingsChartTitle = "Loadings for Component " + Properties.ComponentIndex.ToString();
    }

    private void InvalidateAll()
    {
        InvalidatePcaComponents();
        InvalidatePcaPhases();
    }

    private void InvalidatePcaGrids()
    {
        PcaOneDGridsResults = null;
        PcaTwoDGridsResults = null;
        InvalidatePcaPhases();
    }

    private void InvalidatePcaComponents()
    {
        InvalidatePcaGrids();
        PcaComponentsResults = null;
        InvalidateSelectedComponent();

    }

    private void InvalidatePcaPhases()
    {
        if (PcaColorMap is not null)
        {
            Properties.PcaColorMap = SerializeColorMap(PcaColorMap);
            PcaColorMap = null;
        }
        ClearPcaPhaseIDResults();
    }

    private void SetPcaPhaseIDResults(PhaseIdResults results)
    {
        PcaPhaseIDResults = results;
        WritePhaseDataSection(results);
        UpdateSegmentedChildrenRois();
    }

    private void UpdateSegmentedChildrenRois()
    {
        var phaseNameMap = GetPhaseNamesMapFromExtraData();

        segmentedManager.UpdateSync(
            getSegmentTitle: GetSegmentTitle,
            excludeIds: new[] { byte.MaxValue },
            cancellationToken: default);

        string GetSegmentTitle(byte id)
        {
            return phaseNameMap?.GetValueOrDefault(id) ?? $"Phase {id}";
        }
    }

    private Dictionary<int, string>? GetPhaseNamesMapFromExtraData()
    {
        if (Resources.GetValidIonData() is { } ionData && ionData.Sections.ContainsKey(Resources.DataSectionName))
        {
            var extraDataBytes = ionData.Sections[Resources.DataSectionName].ExtraData;
            var utf8Reader = new Utf8JsonReader(extraDataBytes);
            return JsonSerializer.Deserialize<Dictionary<int, string>>(ref utf8Reader);
        }
        return null;
    }

    private void ClearPcaPhaseIDResults()
    {
        if (PcaPhaseIDResults is not null)
        {
            // Top level ion data should alway be valid, so we can use that to ensure the delete works even if our current
            // ROI IIonData instance might now be extracted. This doesn't matter as it's just deleting a section for the APT file
            Resources.TopLevelNode.GetValidIonData()!.DeleteSection(Resources.DataSectionName);

            PcaPhaseIDResults = null;
            segmentedManager.InvalidateChildren();
        }
    }

    private void DeleteDataSection()
    {
        if (Resources.TopLevelNode.GetValidIonData() is { } ionData)
        {
            ionData.DeleteSection(Resources.DataSectionName);
        }
    }

    private bool WritePhaseDataSection(PhaseIdResults results)
    {
        if (Resources.GetValidIonData() is not { } ionData
            || data?.ExtraData.GridParameters is not { } gridParams)
        {
            return false;
        }

        var minVector = gridParams.GetMinVector();
        var voxelSize = gridParams.GetVoxelSizeDimensions();
        int xBinStride = gridParams.VoxelCount[0];
        int yBinStride = gridParams.VoxelCount[1];

        var binner = new PositionToVoxels(minVector, voxelSize, xBinStride, yBinStride);

        // Clear old section data
        DeleteDataSection();

        // Re-add new empty section
        ionData.AddSection<byte>(Resources.DataSectionName);

        // Add json mapping of ID to user display names as extra data section
        var serializableNameMap = results.PhaseNamesMap().ToDictionary(x => x.Key, x => x.Value.UserDisplayableName());
        ionData.Sections[Resources.DataSectionName].UpdateExtraData(JsonSerializer.SerializeToUtf8Bytes(serializableNameMap));

        // Map all ions to their corresponding voxels, then then map that voxel to a phase ID
        foreach (var chunk in ionData.CreateSectionDataEnumerable(IonDataSectionName.Position, Resources.DataSectionName))
        {
            byte[] buffer = new byte[chunk.Length];
            var positionsMem = chunk.ReadSectionData<Vector3>(IonDataSectionName.Position);
            for (var i = 0; i < chunk.Length; i++)
            {
                Vector3 pos = positionsMem.Span[i];
                int voxel = binner.ToVoxel(pos);
                byte phase = (byte)(results.PhaseForVoxel(new VoxelID(voxel)) ?? byte.MaxValue);
                buffer[i] = phase;
            }
            chunk.WriteSectionData<byte>(Resources.DataSectionName, buffer);
        }
        return true;
    }

    public async Task IncrementComponentIndex(int incr, CancellationToken token)
    {
        if (!numberOfComponents.HasValue) { return; }
        var currentIndex = Properties.ComponentIndex;
        var newIndex = currentIndex + incr;
        if (newIndex >= numberOfComponents.Value)
        {
            newIndex = 0;
        }
        if (newIndex < 0)
        {
            newIndex = numberOfComponents.Value - 1;
        }
        Properties.ComponentIndex = newIndex;
        await UpdateSelectedComponent(token);
    }
}