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
    public int[] VoxelIndices { get; }

    public List<ComponentResults> Components { get; }

    public ComponentsResults(int[] voxelIndices, List<ComponentResults> components)
    {
        VoxelIndices = voxelIndices;
        Components = components;
    }

}