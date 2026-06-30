using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.PcaLib.Interface;
using Cameca.CustomAnalysis.Utilities;
using Cameca.CustomAnalysis.Utilities.Segmentation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        var (gridParams, voxelFeatureMatrix) = CreateVoxelFeatureMatrix(ionData);

        var extraData = new VoxelFeatureMatrixExtraData(gridParams, Properties.GridMethod);
        var data = new VoxelFeatureMatrixSectionData(extraData, voxelFeatureMatrix);

        VoxelFeatureMatrixSerializer.WriteToIonDataSection(ionData, Resources.DataSectionName, data);
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

    protected override void OnPropertiesChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertiesChanged(e); CanSave = true;
        switch (e.PropertyName)
        {
            case nameof(VoxelizationProperties.GridMethod):
                InvalidateAll();
                break;
            case nameof(VoxelizationProperties.VoxelSize):
            case nameof(VoxelizationProperties.VoxelGridEdgeBuffer):
                //if (Properties.GridMethod != GridMethod.Grid3D)
                //{
                    InvalidateAll();
                //}
                break;
            case nameof(VoxelizationProperties.BinSize):
            case nameof(VoxelizationProperties.BinStart):
            case nameof(VoxelizationProperties.BinEnd):
                if (Properties.GridMethod == GridMethod.Bins)
                {
                    InvalidateAll();
                }
                break;
            default:
                break;
        }
    }

    private void InvalidateAll()
    {
        DataStateIsValid = false;
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

    private (GridParameters GridParams, VoxelFeatureMatrix Matrix) CreateVoxelFeatureMatrix(IIonData ionData)
    {
        if (Properties.GridMethod == GridMethod.Grid3D)
        {
            return CreateVoxelFeatureMatrixFrom3DGrid(ionData);
        }
        else
        {
            var featureResolver = CreateFeatureResolver();
            return CreateVoxelFeatureMatrixFromFeatures(ionData, featureResolver);
        }
    }

    private (GridParameters GridParams, VoxelFeatureMatrix Matrix) CreateVoxelFeatureMatrixFrom3DGrid(IIonData ionData)
    {
        if (Resources.GetGrid() is not { } grid)
        {
            throw new InvalidOperationException("3D Grid Method requires presence of 3D Grid node");
        }
        var hostGridData = grid.GetData<IGrid3DData>()!;
        double voxelSize = hostGridData.VoxelSize[0];
        if (voxelSize != hostGridData.VoxelSize[1] || voxelSize != hostGridData.VoxelSize[2])
        {
            throw new InvalidOperationException("All 3D Grid voxel dimensions must be the same");
        }

        var gridStart = new double[]{
                hostGridData.GridRange[0, 0],
                hostGridData.GridRange[1, 0],
                hostGridData.GridRange[2, 0],
            };
        var gridParams = new GridParameters(gridStart, voxelSize, hostGridData.NumVoxels);

        int totalVoxelCount = gridParams.VoxelCount.Aggregate(1, (accu, next) => accu *= next);
        int nFeatureCount = ionData.Ions.Count();
        using var builder = new VoxelFeatureMatrixFrom3DGridBuilder(totalVoxelCount, nFeatureCount);

        for (int i = 0; i < nFeatureCount; i++)
        {
            builder.Update(hostGridData.GetDataForIon(i));
        }

        return (gridParams, builder.Build());

    }

    private (GridParameters GridParams, VoxelFeatureMatrix Matrix) CreateVoxelFeatureMatrixFromFeatures(IIonData ionData, IFeatureResolver featureResolver)
    {
        GridParameters gridParams = Grid3DUtils.CreateGridParameters(
            ionData.Extents,
            Properties.VoxelSize,
            Properties.VoxelGridEdgeBuffer);
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
        return (gridParams, builder.Build());
    }
}

