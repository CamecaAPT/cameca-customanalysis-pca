using Cameca.CustomAnalysis.Interface;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cameca.CustomAnalysis.Pca;

internal class PeakFeatureResolver : IFeatureResolver
{
    private readonly List<RangeMapEntry> rangeMap;
    private ReadOnlyMemory<float> masses;

    public PeakFeatureResolver(IEnumerable<IonTypeInfoRange> ionRanges)
    {
        rangeMap = ionRanges.Select(r => new RangeMapEntry(r.Min, r.Max)).ToList();
    }

    public List<string> RequiredSections { get; } = new List<string>
        {
            IonDataSectionName.Mass,
        };

    public void LoadChunk(IChunkState chunk)
    {
        masses = chunk.ReadSectionData<float>(IonDataSectionName.Mass);
    }

    public int GetFeature(int chunkIndex)
    {
        float mass = masses.Span[chunkIndex];
        for (int r = 0; r < rangeMap.Count; r++)
        {
            var entry = rangeMap[r];
            if (mass >= entry.Min && mass < entry.Max)
            {
                return r;
            }
        }
        return Constants.InvalidFeature;
    }
}

