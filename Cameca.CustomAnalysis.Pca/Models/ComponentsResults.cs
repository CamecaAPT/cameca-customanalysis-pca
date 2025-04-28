using Cameca.CustomAnalysis.Interface;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Cameca.CustomAnalysis.Pca;

public sealed class ComponentsResults
{
    public IGrid3DData Grid3DData { get; }

    public int[] VoxelIndices { get; }

    public List<ComponentResults> Components { get; }

    public ComponentsResults(IGrid3DData grid3DData, int[] voxelIndices, List<ComponentResults> components)
    {
        Grid3DData = grid3DData;
        VoxelIndices = voxelIndices;
        Components = components;

        List<VoxelID> emptyList = new List<VoxelID>();
    }

}