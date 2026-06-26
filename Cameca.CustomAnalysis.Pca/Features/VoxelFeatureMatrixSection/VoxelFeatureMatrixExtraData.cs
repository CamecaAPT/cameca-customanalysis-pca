using Cameca.CustomAnalysis.PcaLib.Interface;
using System.Text.Json.Serialization;

namespace Cameca.CustomAnalysis.Pca;

public sealed class VoxelFeatureMatrixExtraData
{
    [JsonPropertyName("grid_parameters")]
    public GridParameters GridParameters { get; }

    [JsonPropertyName("grid_method")]
    public GridMethod GridMethod { get; }

    [JsonConstructor]
    public VoxelFeatureMatrixExtraData(GridParameters gridParameters, GridMethod gridMethod)
    {
        GridParameters = gridParameters;
        GridMethod = gridMethod;
    }
}
