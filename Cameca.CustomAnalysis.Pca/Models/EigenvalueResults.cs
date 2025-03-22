namespace Cameca.CustomAnalysis.Pca;

internal sealed class EigenvalueResults
{
    public float[] Evals { get; }

    public EigenvalueResults(float[] evals)
    {
        Evals = evals;
    }
}
