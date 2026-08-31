using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using CommunityToolkit.HighPerformance.Buffers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Cameca.CustomAnalysis.Pca;

internal delegate bool CompareIsovalue(float score, float threshold);

internal partial class SelectComponentIsovalueAnalysis : StandardAnalysisFilterNodeBase<SelectComponentIsovalueProperties>
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.SelectComponentIsovalueAnalysis";

    public SelectComponentIsovalueAnalysis(IStandardAnalysisFilterNodeBaseServices services, ResourceFactory resourceFactory) : base(services, resourceFactory)
    {
    }

    private static ReadOnlyDictionary<IsovalueComparison, CompareIsovalue> ComparisonFunctions { get; } = new ReadOnlyDictionary<IsovalueComparison, CompareIsovalue>(
        new Dictionary<IsovalueComparison, CompareIsovalue>
        {
            { IsovalueComparison.GreaterOrEqual, (score, threshold) => score >= threshold },
            { IsovalueComparison.GreaterThan, (score, threshold) => score > threshold },
            { IsovalueComparison.LessOrEqual, (score, threshold) => score <= threshold },
            { IsovalueComparison.LessThan, (score, threshold) => score < threshold },
        });

    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("Select Component by Isovalue");

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    protected override async IAsyncEnumerable<ReadOnlyMemory<ulong>> GetIndicesDelegateAsync(
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        IIonData ownerIonData,
        IProgress<double>? progress,
        [EnumeratorCancellation] CancellationToken token)
    {
        var nullableData = ownerIonData.GetNullableVoxelFeatureMatrixSectionData(Resources.Parent!.DataSectionName, () => DataStateIsError, (errVal) => DataStateIsError = errVal);
        if (nullableData is not { } data)
        {
            DataStateIsError = true;
            yield break;
        }
        DataStateIsError = false;

        var gridParams = data.ExtraData.GridParameters;
        var scoresMatrix = data.VoxelFeatureMatrix;
        var nonEmptyVoxels = scoresMatrix.GetVoxelIndices();
        var componentCount = scoresMatrix.FeatureCount;
        // Properties is 1 indexed for user familiarity and labeling, so need to reindex to zero for selection
        int componentIndex = Properties.ComponentNumber - 1;

        if (componentCount < Properties.ComponentNumber)
        {
            DataStateIsError = true;
            yield break;
        }

        var scores = scoresMatrix.GetFeatureData(componentIndex);

        var minVector = gridParams.GetMinVector();
        var voxelSize = gridParams.GetVoxelSizeDimensions();
        int xBinStride = gridParams.VoxelCount[0];
        int yBinStride = gridParams.VoxelCount[1];

        var binner = new PositionToVoxels(minVector, voxelSize, xBinStride, yBinStride);

        // Create a map of voxel index to the associated score
        var scoredVoxels = new Dictionary<int, float>();
        for (int i = 0; i < nonEmptyVoxels.Length; i++)
        {
            scoredVoxels[nonEmptyVoxels[i]] = scores.Span[i];
        }

        // Build buffers of filtered indices to return
        // Iterating through each point (to determine inclusion) is a bit of a complex chunked iterator code to support >Int32.MaxValue number of ions in a data set
        ulong index = 0ul;
        float threshold = Properties.Isovalue;
        var comparisonFunc = ComparisonFunctions[Properties.Comparison];
        foreach (var chunk in ownerIonData.CreateSectionDataEnumerable(IonDataSectionName.Position))
        {
            int bufferIndex = 0;
            using var buffer = MemoryOwner<ulong>.Allocate(chunk.Length);
            var positions = chunk.ReadSectionData<Vector3>(IonDataSectionName.Position);
            for (int chunkIndex = 0; chunkIndex < chunk.Length; chunkIndex++)
            {
                var bin = binner.ToVoxel(positions.Span[chunkIndex]);
                if (scoredVoxels.TryGetValue(bin, out float score) && comparisonFunc(score, threshold))
                {
                    buffer.Span[bufferIndex++] = index;
                }
                index += 1ul;
            }
            yield return buffer.Slice(0, bufferIndex).Memory;
        }

        DataStateIsValid = true;
    }

    protected override void OnPropertiesChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertiesChanged(e);
        CanSave = true;
        DataStateIsValid = false;
    }

    protected override void OnDataIsValidChanged(bool isValid)
    {
        if (!isValid)
        {
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
}
