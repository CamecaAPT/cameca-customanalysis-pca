using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.PcaLib.Interface;
using Cameca.CustomAnalysis.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;


namespace Cameca.CustomAnalysis.Pca;

internal partial class VoxelizationAnalysis : StandardAnalysisFilterNodeBase<VoxelizationProperties>// DataFilterNodeBase
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.VoxelizationAnalysis";

    public VoxelizationAnalysis(IStandardAnalysisFilterNodeBaseServices services, ResourceFactory resourceFactory) : base(services, resourceFactory)
    {
    }

    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("Voxelization");


    private void Update(IIonData ionData)
    {
        var gridParams = Grid3DUtils.CreateGridParameters(
            ionData.Extents,
            Properties.VoxelSize,
            Properties.VoxelGridEdgeBuffer);
        var featureResolver = CreateFeatureResolver();
        var voxelFeatureMatrix = CreateVoxelFeatureMatrix(ionData, gridParams, featureResolver);

        VoxelFeatureMatrixSerializer.WriteToIonDataSection(
            ionData,
            Resources.DataSectionName,
            gridParams,
            voxelFeatureMatrix.GetVoxelIndices(),
            voxelFeatureMatrix);
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    protected override async IAsyncEnumerable<ReadOnlyMemory<ulong>> GetIndicesDelegateAsync(
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        IIonData ownerIonData,
        IProgress<double>? progress,
        [EnumeratorCancellation] CancellationToken token)
    {
        Update(ownerIonData);
        foreach (var chunk in ownerIonData.AllowAllFilter(progress, token))
        {
            yield return chunk;
        }
    }
    private IFeatureResolver CreateFeatureResolver()
    {
        switch (Properties.GridMethod)
        {
            case GridMethod.IonTypes:
                return new IonTypeFeatureResolver();
            case GridMethod.Peaks:
                var ionRanges = Resources.RangeManager?.GetIonRanges()
                    ?? throw new InvalidOperationException("Requires range information");
                return new PeakFeatureResolver(ionRanges);
            case GridMethod.Bins:
                return new BinnedFeatureResolver(Properties.BinSize, Properties.BinStart, Properties.BinEnd);
            default:
                throw new NotSupportedException($"Grid Method is not supported: {Properties.GridMethod.ToString()}");
        }
    }

    private static VoxelFeatureMatrix CreateVoxelFeatureMatrix(IIonData ionData, GridParameters gridParams, IFeatureResolver featureResolver)
    {

        int totalVoxelCount = gridParams.VoxelCount.Aggregate(1, (accu, next) => accu *= next);
        using var builder = new VoxelFeatureMatrixBuilder(totalVoxelCount, ionData.IonCount);
        var sectionNames = featureResolver.RequiredSections
            .Concat(new string[] { IonDataSectionName.Position })
            .ToArray();
        int voxelsX = gridParams.VoxelCount[0];
        int voxelsY = gridParams.VoxelCount[1];
        int voxelsAll = voxelsX * voxelsY * gridParams.VoxelCount[2];

        foreach (var chunk in ionData.CreateSectionDataEnumerable(sectionNames))
        {
            var buffer = new VoxelFeatureMatrixIon[chunk.Length];
            var positions = chunk.ReadSectionData<Vector3>(IonDataSectionName.Position);
            featureResolver.LoadChunk(chunk);

            for (var i = 0; i < chunk.Length; i++)
            {
                var position = positions.Span[i];

                int voxX = (int)Math.Floor((position.X - gridParams.GridStart[0]) / gridParams.VoxelSize);
                int voxY = (int)Math.Floor((position.Y - gridParams.GridStart[1]) / gridParams.VoxelSize);
                int voxZ = (int)Math.Floor((position.Z - gridParams.GridStart[2]) / gridParams.VoxelSize);

                int voxIndex = voxX + (voxY * voxelsX) + (voxZ * voxelsX * voxelsY);

                int feature = featureResolver.GetFeature(i);
                buffer[i] = new VoxelFeatureMatrixIon(voxIndex, feature);
            }
            builder.Update(buffer);
        }
        return builder.Build();
    }
}

