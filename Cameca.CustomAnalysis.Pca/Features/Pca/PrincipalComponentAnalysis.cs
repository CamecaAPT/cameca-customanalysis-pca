using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Pca.Models;
using Cameca.CustomAnalysis.PcaLib.Interface;
using Cameca.CustomAnalysis.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using PcaLibPrincipalComponentAnalysis = Cameca.CustomAnalysis.PcaLib.Interface.PrincipalComponentAnalysis;

namespace Cameca.CustomAnalysis.Pca;

[DefaultView(PcaViewModel.UniqueId, typeof(PcaViewModel))]
internal partial class PrincipalComponentAnalysis : BasicCustomAnalysisBase<PcaProperties>
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.PrincipalComponentAnalysis";

    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("Principal Component Analysis");

    public ObservableCollection<IRenderData> EigenvalueRenderData { get; } = new();

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
    private IColorMap? componentsColorMap = null;

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
        ResourceFactory resourceFactory)
        : base(services, resourceFactory)
    {
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

        //await UpdateHistograms(cancellationToken);
        //await UpdateGrids(cancellationToken);
        //SelectedLoadingsIndex = 0;
    }

    // After setting the number of components, the components data can be computed. We can follow up with the current
    // selected component data as well using the currently selected component index
    [RelayCommand(CanExecute = nameof(UpdateComponentsCanExecute))]
    public async Task UpdateComponents(CancellationToken cancellationToken)
    {
        await UpdatePcaResults(Properties.NumberOfComponents, cancellationToken);
    }

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
    {
        // This isn't the top level -- .Parent! is safe
        if (VoxelFeatureMatrixSerializer.ReadFromIonDataSection(ionData, Resources.Parent!.DataSectionName) is not { } data)
        {
            throw new InvalidOperationException("Parent must define a VoxelFeatureMatrix");
        }
        return data;
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
}
