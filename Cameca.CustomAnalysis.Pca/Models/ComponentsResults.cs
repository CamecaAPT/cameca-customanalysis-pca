using Cameca.CustomAnalysis.PcaLib.Interface;
using System.Linq;

namespace Cameca.CustomAnalysis.Pca.Models;

public sealed class ComponentsResults
{
    public GridParameters GridParams { get; }

    public int[] VoxelIndices { get; }

    public ComponentDataModel[] Components { get; }

    public ComponentsResults(GridParameters gridParams, int[] voxelIndices, ComponentData[] components)
    {
        GridParams = gridParams;
        VoxelIndices = voxelIndices;
        Components = components
            .Select((x, i) => new ComponentDataModel($"Component {i+1}", x.Scores, x.Loads))
            .ToArray();
    }

}