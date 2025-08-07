using System;

namespace Cameca.CustomAnalysis.Pca.Utils;

internal static class RandomExtensions
{
    public static float NextSingleNormal(this Random random, float mean = 0, float stdDev = 1)
    {
        float u1 = 1f - random.NextSingle();
        float u2 = 1f - random.NextSingle();
        float randStdNormal = MathF.Sqrt(-2f * MathF.Log(u1)) * MathF.Sin(2f * MathF.PI * u2);
        return mean + stdDev * randStdNormal;
    }
}
