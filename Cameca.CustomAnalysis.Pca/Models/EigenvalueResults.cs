namespace Cameca.CustomAnalysis.Pca;

internal sealed class EigenvalueResults
{
    public float[] Evals { get; }
    public int Rank { get; }
    public float[] NoiseEvals { get; }

    public EigenvalueResults(float[] evals, int rank, float[] noiseEvals)
    {
        Evals = evals;
        Rank = rank;
        NoiseEvals = noiseEvals;
    }
}
