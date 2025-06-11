using System.Collections.Generic;
using System.Linq;

namespace Cameca.CustomAnalysis.Pca;

internal static class EnumerableUInt64Extensions
{
    public static ulong Sum(this IEnumerable<ulong> values)
    {
        return values.Aggregate(0ul, (a, c) => a + c);
    }
}