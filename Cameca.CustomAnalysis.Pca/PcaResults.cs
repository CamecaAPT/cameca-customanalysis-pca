using Cameca.CustomAnalysis.Interface;
using System.Collections.Generic;

namespace Cameca.CustomAnalysis.Pca;

internal sealed class EigenvalueResults
{
    public float[] Evals { get; }

    public EigenvalueResults(float[] evals)
    {
        Evals = evals;
    }
}


internal sealed class ComponentResults
{
    public float[] Scores { get; }
    public float[] Loads { get; }

    public ComponentResults(float[] scores, float[] loads)
    {
        Scores = scores;
        Loads = loads;
    }
}

internal sealed class ComponentsResults
{
    public IGrid3DData Grid3DData { get; }

    public int[] VoxelIndices { get; }

    public List<ComponentResults> Components { get; }

    public ComponentsResults(IGrid3DData grid3DData, int[] voxelIndices, List<ComponentResults> components)
    {
        Grid3DData = grid3DData;
        VoxelIndices = voxelIndices;
        Components = components;
    }

}