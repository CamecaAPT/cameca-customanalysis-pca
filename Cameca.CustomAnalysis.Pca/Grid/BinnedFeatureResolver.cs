using Cameca.CustomAnalysis.Interface;
using System;
using System.Collections.Generic;

namespace Cameca.CustomAnalysis.Pca;

internal class BinnedFeatureResolver : IFeatureResolver
{
    private readonly double binSize;
    private readonly float binStart;
    private readonly float? binEnd;
    private ReadOnlyMemory<float> masses;

    public BinnedFeatureResolver(double binSize, float? binStart, float? binEnd)
    {
        this.binSize = binSize;
        this.binStart = binStart ?? 0f;
        this.binEnd = binEnd.HasValue
            ? (float)Math.Ceiling(((double)binEnd.Value - this.binStart) / this.binSize)
            : null;
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
        var mass = masses.Span[chunkIndex];
        if (mass < binStart || (binEnd.HasValue && mass >= binEnd))
        {
            return Constants.InvalidFeature;
        }
        return (int)Math.Floor(mass / binSize);
    }
}

