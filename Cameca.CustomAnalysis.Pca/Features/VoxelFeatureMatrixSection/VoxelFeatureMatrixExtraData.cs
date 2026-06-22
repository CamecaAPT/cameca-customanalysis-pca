using Cameca.CustomAnalysis.PcaLib.Interface;
using System.Text.Json.Serialization;

namespace Cameca.CustomAnalysis.Pca;

public sealed class VoxelFeatureMatrixExtraData
{
    public GridParameters GridParameters { get; }
    public int NonEmptyVoxels { get; }

    [JsonConstructor]
    public VoxelFeatureMatrixExtraData(GridParameters gridParameters, int nonEmptyVoxels)
    {
        GridParameters = gridParameters;
        NonEmptyVoxels = nonEmptyVoxels;
    }
}
