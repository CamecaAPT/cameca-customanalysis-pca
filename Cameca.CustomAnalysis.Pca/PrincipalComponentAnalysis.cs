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

namespace Cameca.CustomAnalysis.Pca;

[DefaultView(PcaViewModel.UniqueId, typeof(PcaViewModel))]
internal partial class PrincipalComponentAnalysis : BasicCustomAnalysisBase<PcaProperties>, IGridsUsageDelegate, IHistogramsUsageDelegate
{
    private readonly INodeDataProvider nodeDataProvider;
    private readonly IOptionsAccessor optionsAccessor;
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.PcaNode";

    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("Principal Component Analysis");

    public ObservableCollection<IRenderData> EigenvalueRenderData { get; } = new();

    [ObservableProperty]
    private ICollection<IRenderData> componentRenderData = Array.Empty<IRenderData>();

    [ObservableProperty]
    private ICollection<IRenderData> pcaPhasesRenderData = Array.Empty<IRenderData>();

    [ObservableProperty]
    private IColorMap? pcaColorMap = null;

    [ObservableProperty]
    private IColorMap? componentsColorMap = null;

    [ObservableProperty]
    public ICollection<IRenderData> scoresHistogramRenderData = Array.Empty<IRenderData>();

    [ObservableProperty]
    private ICollection<IRenderData> selectedGridRenderData = Array.Empty<IRenderData>();

    [ObservableProperty]
    private IGridsUsageDelegate gridsUsageDelegate = new DoNothingGridsUsageDelegate();

    [ObservableProperty]
    private IHistogramsUsageDelegate histogramsUsageDelegate = new DoNothingHistogramsUsageDelegate();

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
    public double loadingsLabelsRotation = 0d;

    public bool UpdateComponentsCanExecute => PcaComponentsResults is null;
    public bool UpdateGridsCanExecute => PcaTwoDGridsResults is null;
    public bool UpdatePCAPhasesCanExecute => PcaPhaseIDResults is null;

    public bool UpdateRankEstimationCanExecute => NoiseEigenvalueResults is null;

    public bool UpdateSelectedComponentCanExecute =>
        !LoadingsSeries.Any() || !LoadingsLabels.Any() || !ScoresHistogramRenderData.Any();

    public Func<double, string> AxisYLabelFormatter { get; } = (double value) => value.ToString("F3");

    internal HashSet<string> gridsToUseForPCA = new();
    internal HashSet<string> histogramsToUseForPCA = new();
    public PrincipalComponentAnalysis(
        IStandardAnalysisFilterNodeBaseServices services,
        ResourceFactory resourceFactory,
        INodeDataProvider nodeDataProvider,
        IOptionsAccessor optionsAccessor)
        : base(services, resourceFactory)
    {
        this.nodeDataProvider = nodeDataProvider;
        this.optionsAccessor = optionsAccessor;
        this.gridsUsageDelegate = this;
        this.histogramsUsageDelegate = this;
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
        if (eventArgs.Trigger != EventTrigger.Load)
        {
            Properties.LogScaleY = optionsAccessor.GetOptions<PcaGlobalOptions>().IsLogScaleDefault;
        }
    }

    protected override byte[]? GetSaveContent()
    {
        if (PcaColorMap is not null)
        {
            Properties.PcaColorMap = SerializeColorMap(PcaColorMap);
        }
        if (ComponentsColorMap is not null)
        {
            Properties.ComponentsColorMap = SerializeColorMap(ComponentsColorMap);
        }
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
        if (await GetEigenvalueResults(cancellationToken) is { } results)
        {
            EigenvalueResults = results;
            NoiseEigenvalueResults = PcaCalculator.GetNoiseEigenvalues(
                results.Evals,
                Properties.Gaps,
                (int)Properties.Significance,
                Properties.Refine);
            return true;
        }
        return false;
    }

    private async Task<EigenvalueResults?> GetEigenvalueResults(CancellationToken cancellationToken)
    {
        if (await Resources.GetIonData(cancellationToken: cancellationToken) is not { } ionData)
        {
            return null;
        }

        var gridNode = Resources.GetGrid();
        if (await GetGridData(gridNode, cancellationToken) is not IGrid3DData gridData)
        {
            return null;
        }

        return PcaCalculator.GetEignevalues(
            ionData,
            gridData,
            nFeatures: GetMaxGrid3DDataIndex(gridData));
    }

    /// <summary>
    /// <see cref="IGrid3DData"/> assumes number of Ions defined the max index, but as we're repurposing
    /// it a bit, we need to use some other method of identifying how many channels it supports.
    /// A naive solution is just try until we get an exception and use that max value.
    /// </summary>
    /// <param name="gridData"></param>
    /// <returns></returns>
    private static int GetMaxGrid3DDataIndex(IGrid3DData gridData)
    {
        int max = 0;
        try
        {
            while (true)
            {
                gridData.GetDataForIon(max);
                max++;
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            return max;
        }
        catch
        {
            throw;
        }
    }

    [RelayCommand(CanExecute = nameof(UpdateRankEstimationCanExecute))]
    public async Task UpdateRankEstimation(CancellationToken cancellationToken)
    {
        var eigenvalueResults = EigenvalueResults ??= await GetEigenvalueResults(cancellationToken);
        if (eigenvalueResults is not null)
        {
            NoiseEigenvalueResults = PcaCalculator.GetNoiseEigenvalues(
                eigenvalueResults.Evals,
                Properties.Gaps,
                (int)Properties.Significance,
                Properties.Refine);
        }
    }

    // After setting the number of components, the components data can be computed. We can follow up with the current
    // selected component data as well using the currently selected component index
    [RelayCommand(CanExecute = nameof(UpdateComponentsCanExecute))]
    public async Task UpdateComponents(CancellationToken cancellationToken)
    {
        DataStateIsError = false;
        if (await Resources.GetIonData(cancellationToken: cancellationToken) is not { } ionData)
        {
            DataStateIsError = true;
            return;
        }

        var gridNode = Resources.GetGrid();
        if (await GetGridData(gridNode, cancellationToken) is not IGrid3DData gridData)
        {
            DataStateIsError = true;
            return;
        }

        PcaComponentsResults = PcaCalculator.GetComponents(
            gridData,
            ionData,
            GetMaxGrid3DDataIndex(gridData),
            Properties.NumberOfComponents);

        UpdateOptionsBounds();

        // Ensure that the selected component falls in the valid range of number of components
        if (Properties.PcaPhaseIndex < 0)
        {
            Properties.ComponentIndex = 0;
        }
        else if (Properties.ComponentIndex > Properties.NumberOfComponents)
        {
            Properties.ComponentIndex = Properties.NumberOfComponents;
        }

        await UpdateHistograms(cancellationToken);
        await UpdateGrids(cancellationToken);
        await UpdateSelectedComponent(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(UpdateGridsCanExecute))]
    public async Task UpdateHistograms(CancellationToken cancellationToken)
    {
        DataStateIsError = false;
        if (await Resources.GetIonData(cancellationToken: cancellationToken) is not { } ionData)
        {
            DataStateIsError = true;
            return;
        }

        var gridNode = Resources.GetGrid();
        if (await GetGridData(gridNode, cancellationToken) is not IGrid3DData gridData)
        {
            DataStateIsError = true;
            return;
        }

        if (PcaComponentsResults is null)
        {
            await UpdateComponents(cancellationToken);
        }

        var componentsResults = PcaComponentsResults;
        if (componentsResults is null)
        {
            return;
        }
        // Scores Histogram
        List<IRenderData> newHistogramsData = new();
        int componentIndex = 0;

        foreach (ComponentResults componentResults in componentsResults.Components)
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
            var scoresHistogram = Resources.ChartObjects.CreateHistogram (scoreData, color: Colors.Blue);
            scoresHistogram.Name = GridID.GridLetterForIndex(componentIndex);
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
        DataStateIsError = false;
        if (await Resources.GetIonData(cancellationToken: cancellationToken) is not { } ionData)
        {
            DataStateIsError = true;
            return;
        }

        var gridNode = Resources.GetGrid();
        if (await GetGridData(gridNode, cancellationToken) is not IGrid3DData gridData)
        {
            DataStateIsError = true;
            return;
        }

        if (PcaComponentsResults is null)
        {
           await UpdateComponents(cancellationToken);
        }

        var compResults = PcaComponentsResults;
        if (compResults != null)
        {
            var pcaPhaseIdProperties = new PcaPhaseIdentificationProperties(Properties.GridProjectionBinSize, Properties.GridProjectionDelocalization, Properties.NoiseFloorFraction, Properties.PeakSummitAllowance, Properties.NumberOfComponents);
            ScoresGrid = PcaCalculator.GenerateScoresGrid(ionData, compResults, pcaPhaseIdProperties);
            PcaTwoDGridsResults = PcaCalculator.CalculateTwoDGrids(ScoresGrid, pcaPhaseIdProperties);
            PcaOneDGridsResults = PcaCalculator.CalculateOneDGrids(ScoresGrid, pcaPhaseIdProperties);
        }
    }

    // Uses the component data (or computes for all componets if necessary) to generate plots for the selected component by index
    [RelayCommand(CanExecute = nameof(UpdateSelectedComponentCanExecute))]
    public async Task UpdateSelectedComponent(CancellationToken cancellationToken)
    {
        LoadingsLabels = Array.Empty<string>();

        if (await Resources.GetIonData(cancellationToken: cancellationToken) is not { } ionData)
        {
            DataStateIsError = true;
            return;
        }

        int numComponents = Properties.NumberOfComponents;
        int selectedIndex = Properties.ComponentIndex;

        if (PcaComponentsResults is null)
        {
            await UpdateComponents(cancellationToken);
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

        switch (Properties.GridMethod)
        {
            case GridMethod.IonTypes:
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
            default:
                break;
        }

        
   }

    // PCA Phases can be updated independently of the Components and grids   if the number of grids to use changes
    // or if any of the properties to use in the calculation change
    [RelayCommand(CanExecute = nameof(UpdatePCAPhasesCanExecute))]
    public async Task UpdatePCAPhases(CancellationToken cancellationToken)
    {
        var scoresGrid = ScoresGrid;
        var oneDGridsResults = PcaOneDGridsResults;
        var twoDGridsResults = PcaTwoDGridsResults;

        if ((scoresGrid != null) && (oneDGridsResults != null) && (twoDGridsResults != null))
        {
            var pcaPhaseIdProperties = new PcaPhaseIdentificationProperties(Properties.GridProjectionBinSize, Properties.GridProjectionDelocalization, Properties.NoiseFloorFraction, Properties.PeakSummitAllowance, Properties.NumberOfComponents);
            PcaPhaseIDResults = scoresGrid.GetPhasesStrategyF(pcaPhaseIdProperties, gridsToUseForPCA, histogramsToUseForPCA);
        }
    }

    // Updates the noise eigenvalues tab plot when the computed eigenvalue data changes
    partial void OnEigenvalueResultsChanged(EigenvalueResults? value)
    {
        EigenvalueRenderData.Clear();
        if (EigenvalueResults is not { Evals: { } evals })
        {
            return;
        }
        var positions = evals.Select((value, index) => new Vector3(index, 0f, value)).ToArray();
        var series = Resources.ChartObjects.CreateSeries();
        series.Name = "Eigenvalues";
        series.Positions = positions;
        series.Color = Colors.Blue;
        series.MarkerShape = MarkerShape.Circle;
        series.MarkerColor = Colors.Blue;

        EigenvalueRenderData.Add(series);
    }

    // Updates the components 3D plots when the component data (derived from selected number of components) changes
    static float[] GetPhaseIdScoresForVoxelIndices(PhaseIdResults phaseIdResults, int compIndex, int[] voxelIndices)
    {
        int numIndices = voxelIndices.Length;
        float[] scores = new float[numIndices];
        for (int i = 0; i < numIndices; ++i)
        {
            int voxelIndex = voxelIndices[i];
            scores[i] = phaseIdResults.PhaseForVoxelIntValue(voxelIndex) == compIndex ? 1.0f : 0.0f ;
        }
        return scores;
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
                .Select(index => new Vector3(index, 0f, noiseEvals[index - rank]))
                .ToArray();
            var noiseSeries = Resources.ChartObjects.CreateSeries();
            noiseSeries.Name = "Noise Eigenvalues";
            noiseSeries.Positions = noisePositions;
            noiseSeries.Color = Colors.Red;
            noiseSeries.MarkerShape = MarkerShape.None;

            EigenvalueRenderData.Add(noiseSeries);
        }
    }

    // Updates the components 3D plots when the component data (derived from selected number of components) changes
    partial void OnPcaPhaseIDResultsChanged(PhaseIdResults? value)
    {
        if (PcaPhaseIDResults is { IdentifiedPhase: Dictionary<VoxelID, int> identifiedPhase,
            PhaseIndexMap: Dictionary<PcaPhaseName, int> phaseIndexMap } )

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
                var positionsWithValues = PositionScores.GetScoredPositions(PcaComponentsResults.Grid3DData, voxelIndices, phaseIdScores, jitterStdDev: jitterStdDev);

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
                int binCount = (1 + max - min) ;
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
            SelectedGridRenderData = Array.Empty<IRenderData>();
            int gridCount = twoDPeakProjections.Count;
            var newGridProjectionsData = new List<IRenderData>();
            foreach (KeyValuePair<TwoDGridID, TwoDPeakProjection> kvp in twoDPeakProjections)
            {
                var histogram = Resources.ChartObjects.CreateHistogram2D();
                histogram.Name = kvp.Key.ToString();
                histogram.ColorMap = Resources.ColorMap.GetPresetColorMap(ColorMapPreset.GreyScale);
                FillRenderDataWithGridData(histogram, kvp.Value);
                newGridProjectionsData.Add(histogram);
            }
            SelectedGridRenderData = newGridProjectionsData;
        }
    }
    // Updates the components 3D plots when the component data (derived from selected number of components) changes
    partial void OnPcaComponentsResultsChanged(ComponentsResults? value)
    {
        ComponentRenderData = Array.Empty<IRenderData>();
        SelectedGridRenderData = Array.Empty<IRenderData>();

        if (PcaComponentsResults is not { Grid3DData: { } gridData,
            Components: { } components,
            VoxelIndices: { } voxelIndices }
         || Resources.GetValidIonData() is not { } ionData)
        {
            return;
        }

        int numComponents = Properties.NumberOfComponents;
        int selectedIndex = Properties.ComponentIndex;

        var jitterStdDev = optionsAccessor.GetOptions<PcaGlobalOptions>().JitterStdDev;

        var newComponentsData = new IRenderData[numComponents];
        IValuePointsRenderData? rootValuePoints = null;
        for (int compIndex = 0; compIndex < numComponents; compIndex++)
        {
            var scores = components[compIndex].Scores;
            var positionsWithValues = PositionScores.GetScoredPositions(gridData, voxelIndices, scores, jitterStdDev: jitterStdDev);

            var valuePoints = Resources.ChartObjects.CreateValuePoints();
            valuePoints.Name = $"Component {compIndex}";
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

        return new (mean - 2 * stdDev, mean + 2 * stdDev);
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
        Properties.Min = selectedResult?.Scores.Min();
        Properties.Max = selectedResult?.Scores.Max();
    }

    // Applies filter to the custom analysis: returns the ions in voxels for which the score of the selected component exceeds the specified threshold value
    // Olof Note -- this is where to change logic for viewing results -- redirect the algorithm here to look at the new 'identified phase' structure
  
    protected override async IAsyncEnumerable<ReadOnlyMemory<ulong>> GetIndicesDelegateAsync(IIonData ionData, IProgress<double>? progress, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        DataStateIsError = false;
        if (PcaComponentsResults is null)
        {
            await UpdateComponents(cancellationToken);
        }

        // Extract necessary data out of ComponentsResults using some pattern matching for null checks and variable assignment
        if (PcaComponentsResults is not { Grid3DData: { } gridData, VoxelIndices: { } nonEmptyVoxels }
            || PcaComponentsResults.Components[Properties.ComponentIndex] is not { Scores: { } scores })
        {
            DataStateIsError = true;
            yield break;
        }

        var minVector = gridData.GetMinVector();
        var voxelSize = gridData.GetVoxelSizeDimensions();
        int xBinStride = gridData.NumVoxels[0];
        int yBinStride = gridData.NumVoxels[1];

        var binner = new PositionToVoxels(minVector, voxelSize, xBinStride, yBinStride);
        var phaseIds = PcaPhaseIDResults;

        if (Properties.UsePCAPhaseForDetatchedROI && (phaseIds != null))
        {
            int pcaPhaseOfInterest = Properties.PcaPhaseIndex;

            // Build buffers of filtered indices to return
            // Iterating through each point (to determine inclusion) is a bit of a complex chunked iterator code to support >Int32.MaxValue number of ions in a data set
            ulong index = 0ul;
            foreach (var chunk in ionData.CreateSectionDataEnumerable(IonDataSectionName.Position))
            {
                int bufferIndex = 0;
                using var buffer = MemoryOwner<ulong>.Allocate(chunk.Length);
                var positions = chunk.ReadSectionData<Vector3>(IonDataSectionName.Position);
                for (int chunkIndex = 0; chunkIndex < chunk.Length; chunkIndex++)
                {
                    int bin = binner.ToVoxel(positions.Span[chunkIndex]);

                    int? maybePhase = phaseIds.PhaseForVoxelIntValue(bin);
                  
                    // Properties.ComponentIndex is the selectedComponent
                    if ((maybePhase != null) && (maybePhase.Value == pcaPhaseOfInterest))
                    {
                        buffer.Span[bufferIndex++] = index;
                    }
                    index += 1ul;
                }
                yield return buffer.Slice(0, bufferIndex).Memory;
            }
        }
        else 
        {

            // Create a map of voxel index to the associated score
            var scoredVoxels = nonEmptyVoxels
                .Zip(scores)
                .ToDictionary(x => x.First, x => x.Second);

            // Build buffers of filtered indices to return
            // Iterating through each point (to determine inclusion) is a bit of a complex chunked iterator code to support >Int32.MaxValue number of ions in a data set
            ulong index = 0ul;
            float threshold = Properties.Isovalue;
            foreach (var chunk in ionData.CreateSectionDataEnumerable(IonDataSectionName.Position))
            {
                int bufferIndex = 0;
                using var buffer = MemoryOwner<ulong>.Allocate(chunk.Length);
                var positions = chunk.ReadSectionData<Vector3>(IonDataSectionName.Position);
                for (int chunkIndex = 0; chunkIndex < chunk.Length; chunkIndex++)
                {
                    var bin = binner.ToVoxel(positions.Span[chunkIndex]);
                    if (scoredVoxels.TryGetValue(bin, out float score) && score >= threshold)
                    {
                        buffer.Span[bufferIndex++] = index;
                    }
                    index += 1ul;
                }
                yield return buffer.Slice(0, bufferIndex).Memory;
            }
        }

        DataStateIsValid = true;
    }

    // On Properties panel changes, some data must be invalidated to be recomputed with new values. Invalidations depend on the properties changed
    protected override void OnPropertiesChanged(PropertyChangedEventArgs e)
    {
        // Base call invalidates the returned filter - this correctly needs to be invalidated for any of the current editable properties
        base.OnPropertiesChanged(e);
        CanSave = true;
        switch (e.PropertyName)
        {
            case nameof(PcaProperties.GridMethod):
            case nameof(PcaProperties.VoxelSize):
            case nameof(PcaProperties.VoxelGridEdgeBuffer):
                InvalidateAll();
                break;
            case nameof(PcaProperties.NumberOfComponents):
                if (Properties.NumberOfComponents == 0)
                {
                    NoiseEigenvalueResults = null;
                }
                InvalidatePcaComponents();
                break;
            case nameof(PcaProperties.ComponentIndex):
                UpdateOptionsBounds();
                InvalidateSelectedComponent();
                break;
            case nameof(PcaProperties.Invert):
                FilterIsInverted = Properties.Invert;
                InvalidateSelectedComponent();
                break;
            case nameof(PcaProperties.GridProjectionBinSize):
                InvalidatePcaGrids();
                break;
            case nameof(PcaProperties.GridProjectionDelocalization):
                InvalidatePcaGrids();
                break; 
            case nameof(PcaProperties.NoiseFloorFraction):
                InvalidatePcaGrids();
                break;
            case nameof(PcaProperties.PeakSummitAllowance):
                InvalidatePcaGrids();
                break;
            default:
                break;
        }
    }

    /* Data invalidation methods */
    private void InvalidateSelectedComponent()
    {
        LoadingsSeries = new SeriesCollection();
        LoadingsLabels = Array.Empty<string>();
    }

    private void InvalidateAll()
    {
        Properties.NumberOfComponents = 0;
        NoiseEigenvalueResults = null;
        EigenvalueResults = null;
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
        if (ComponentsColorMap is not null)
        {
            Properties.ComponentsColorMap = SerializeColorMap(ComponentsColorMap);
            ComponentsColorMap = null;
        }

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
        PcaPhaseIDResults = null;
    }

    // Should actually be implemented in the base class CoreNodeBase along with existing DataStateIsValid.
    // Remove after a Cameca.CustomAnalysis.Utilities updates adds this functionality
    protected bool DataStateIsError
    {
        get => DataState?.IsErrorState ?? false;
        set
        {
            if (DataState is not null)
            {
                DataState.IsErrorState = value;
            }
        }
    }

    private async Task<IGrid3DData?> GetGridData(INodeResource? gridNode, CancellationToken cancellationToken)
    {
        if (gridNode is null)
        {
            return null;
        }
        if (await Resources.GetIonData(cancellationToken: cancellationToken) is IIonData ionData)
        {
            switch (Properties.GridMethod)
            {
                case GridMethod.IonTypes:
                    return await Grid3DUtils.CreateIonGrid3DData(
                        Resources,
                        ionData,
                        new double[] {
                            Properties.VoxelSize,
                            Properties.VoxelSize,
                            Properties.VoxelSize,
                        },
                        edgeBuffer: Properties.VoxelGridEdgeBuffer,
                        cancellationToken: cancellationToken);
                case GridMethod.Peaks:
                    return await Grid3DUtils.CreatePeakGrid3DData(
                        Resources,
                        ionData,
                        new double[] {
                            Properties.VoxelSize,
                            Properties.VoxelSize,
                            Properties.VoxelSize,
                        },
                        edgeBuffer: Properties.VoxelGridEdgeBuffer,
                        cancellationToken: cancellationToken);
                default:
                    break;
            }
            
        }
        return null;
    }
}
