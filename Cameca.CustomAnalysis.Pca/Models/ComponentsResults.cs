using Cameca.CustomAnalysis.PcaLib.Interface;

namespace Cameca.CustomAnalysis.Pca.Models;

public sealed class ComponentsResults
{
    public GridParameters GridParams { get; }

    public int[] VoxelIndices { get; }

    public ComponentData[] Components { get; }

    public ComponentsResults(GridParameters gridParams, int[] voxelIndices, ComponentData[] components)
    {
        GridParams = gridParams;
        VoxelIndices = voxelIndices;
        Components = components;
    }

}