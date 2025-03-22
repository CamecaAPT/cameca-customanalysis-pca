namespace Cameca.CustomAnalysis.Pca;

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
