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

namespace Cameca.CustomAnalysis.Pca;

[DefaultView(PcaViewModel.UniqueId, typeof(PcaViewModel))]
internal class PcaNode : AnalysisFilterNodeBase
{
    private readonly ResourceFactory resourceFactory;
    private readonly INodeDataProvider nodeDataProvider;

    public const string UniqueId = "Cameca.CustomAnalysis.Pca.PcaNode";
    
    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("Principal Component Analysis");

    private PcaResults? data;
    public PcaResults? Data
    {
        get => data;
        set
        {
            data = value;
            DataStateIsValid = data is not null;
        }
    }

    public PcaOptions Options { get; private set; } = new();

    public Guid Id => InstanceId;

    public PcaNode(
        IAnalysisFilterNodeBaseServices services,
        ResourceFactory resourceFactory,
        INodeDataProvider nodeDataProvider)
        : base(services)
    {
        this.resourceFactory = resourceFactory;
        this.nodeDataProvider = nodeDataProvider;
    }

    protected override void OnAdded(NodeAddedEventArgs eventArgs)
    {
        base.OnAdded(eventArgs);
        Properties = Options;
        Options.PropertyChanged += (s, e) =>
        {
            DataStateIsValid = false;
        };
        AllowOverrideRanges = true;
    }

    protected override void OnDataIsValidChanged(bool isValid)
    {
        if (!isValid)
        {
            Data = null;
        }
    }

    internal async Task RunPcaFromGrid3D(int nComponents)
    {
        var resources = resourceFactory.CreateResource(InstanceId);
        if (await resources.GetIonData() is not { } ionData)
        {
            throw new InvalidOperationException("No IIonData");
        }

        // Throws InvalidOperationException if not able to resolve a single correct Grid3D node
        var gridNode = resources.FindGridNode(nodeDataProvider);

        if (await nodeDataProvider.Resolve(gridNode.Id)!.GetData(typeof(IGrid3DData)) is not IGrid3DData gridData)
        {
            throw new InvalidOperationException("No IGrid3DData");
        }

        ulong rangedCount = ionData.GetIonTypeCounts().Values.Sum();
        int nIons = (int)rangedCount;

        // Remove empty voxels
        int nAllVoxels = gridData.NumVoxels[0] * gridData.NumVoxels[1] * gridData.NumVoxels[2];
        int nFeatures = ionData.Ions.Count;

        var localBuffer = Enumerable.Range(0, nFeatures)
            .Select(ionIndex => gridData.GetDataForIon(ionIndex).ToArray())
            .ToArray();

        var nonEmptyVoxels = new List<int>();
        for (int voxelIndex = 0; voxelIndex < nAllVoxels; voxelIndex++)
        {
            for (int ionIndex = 0; ionIndex < nFeatures; ionIndex++)
            {
                if (localBuffer[ionIndex][voxelIndex] != 0f)
                {
                    nonEmptyVoxels.Add(voxelIndex);
                    break;
                }
            }
        }
        int nVoxels = nonEmptyVoxels.Count;

        var dataBuffer = new float[nFeatures * nVoxels];
        for (int featureIndex = 0; featureIndex < nFeatures; featureIndex++)
        {
            int x = 0;
            foreach (int voxelIndex in nonEmptyVoxels)
            {
                dataBuffer[(featureIndex * nVoxels) + x++] = localBuffer[featureIndex][voxelIndex];
            }
        }

        int nevals = nFeatures;  // Input?


        // Allocated output buffers
        float[] scores = new float[nVoxels * nComponents];
        float[] loads = new float[nFeatures * nComponents];
        float[] evals = new float[nevals];

        // Call the doPCA function
        PcaLib.doPCA(nVoxels, nFeatures, dataBuffer, nIons, nComponents, nevals, scores, loads, evals);

        Data = new PcaResults(scores, nonEmptyVoxels.ToArray(), loads, evals, gridData.NumVoxels, gridData.VoxelSize, gridData.GridDelta);
    }

    protected override async IAsyncEnumerable<ReadOnlyMemory<ulong>> GetIndicesDelegateAsync(IIonData ionData, IProgress<double>? progress, [EnumeratorCancellation] CancellationToken token)
    {
        int nComponents = Options.Components;
        var resources = resourceFactory.CreateResource(InstanceId);

        // Throws InvalidOperationException if not able to resolve a single correct Grid3D node
        var gridNode = resources.FindGridNode(nodeDataProvider);

        if (await nodeDataProvider.Resolve(gridNode.Id)!.GetData(typeof(IGrid3DData)) is not IGrid3DData gridData)
        {
            throw new InvalidOperationException("No IGrid3DData");
        }

        ulong rangedCount = ionData.GetIonTypeCounts().Values.Sum();
        int nIons = (int)rangedCount;

        // Remove empty voxels
        int nAllVoxels = gridData.NumVoxels[0] * gridData.NumVoxels[1] * gridData.NumVoxels[2];
        int nFeatures = ionData.Ions.Count;

        var localBuffer = Enumerable.Range(0, nFeatures)
            .Select(ionIndex => gridData.GetDataForIon(ionIndex).ToArray())
            .ToArray();

        var nonEmptyVoxels = new List<int>();
        for (int voxelIndex = 0; voxelIndex < nAllVoxels; voxelIndex++)
        {
            for (int ionIndex = 0; ionIndex < nFeatures; ionIndex++)
            {
                if (localBuffer[ionIndex][voxelIndex] != 0f)
                {
                    nonEmptyVoxels.Add(voxelIndex);
                    break;
                }
            }
        }
        int nVoxels = nonEmptyVoxels.Count;

        var dataBuffer = new float[nFeatures * nVoxels];
        for (int featureIndex = 0; featureIndex < nFeatures; featureIndex++)
        {
            int x = 0;
            foreach (int voxelIndex in nonEmptyVoxels)
            {
                dataBuffer[(featureIndex * nVoxels) + x++] = localBuffer[featureIndex][voxelIndex];
            }
        }

        int nevals = nFeatures;  // Input?


        // Allocated output buffers
        float[] scores = new float[nVoxels * nComponents];
        float[] loads = new float[nFeatures * nComponents];
        float[] evals = new float[nevals];

        // Call the doPCA function
        PcaLib.doPCA(nVoxels, nFeatures, dataBuffer, nIons, nComponents, nevals, scores, loads, evals);

        var minVector = new Vector3(
            (float)gridData.GridRange[0, 0],
            (float)gridData.GridRange[1, 0],
            (float)gridData.GridRange[2, 0]);
        var voxelSize = new Vector3(
        (float)gridData.VoxelSize[0],
            (float)gridData.VoxelSize[1],
            (float)gridData.VoxelSize[2]);
        var binner = new Binner(minVector, voxelSize, gridData.NumVoxels[0], gridData.NumVoxels[1]);

        var scoredVoxels = nonEmptyVoxels
            .Zip(scores.Skip(Options.ComponentIndex * nonEmptyVoxels.Count).Select(x => x * 1000f))
            .ToDictionary(x => x.First, x => x.Second);

        ulong index = 0ul;
        float threshold = Options.Isovalue;
        foreach (var chunk in ionData.CreateSectionDataEnumerable(IonDataSectionName.Position))
        {
            int bufferIndex = 0;
            using var buffer = MemoryOwner<ulong>.Allocate(chunk.Length);
            var positions = chunk.ReadSectionData<Vector3>(IonDataSectionName.Position);
            for (int chunkIndex = 0; chunkIndex < chunk.Length; chunkIndex++)
            {
                var bin = binner.ToBin(positions.Span[chunkIndex]);
                if (scoredVoxels.TryGetValue(bin, out float score) && score >= threshold)
                {
                    buffer.Span[bufferIndex++] = index;
                }
                index += 1ul;
            }
            yield return buffer.Slice(0, bufferIndex).Memory;
        }
    }
}
