using Cameca.CustomAnalysis.PcaLib.Interface;
using System.Text.Json.Serialization;

namespace Cameca.CustomAnalysis.Pca;

public sealed class VoxelFeatureMatrixExtraData
{
    [JsonPropertyName("grid_parameters")]
    public GridParameters GridParameters { get; }

    [JsonConstructor]
    public VoxelFeatureMatrixExtraData(GridParameters gridParameters)
    {
        GridParameters = gridParameters;
    }
}
