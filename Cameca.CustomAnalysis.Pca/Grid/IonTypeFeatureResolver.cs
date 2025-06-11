using Cameca.CustomAnalysis.Interface;
using System;
using System.Collections.Generic;

namespace Cameca.CustomAnalysis.Pca;

internal class IonTypeFeatureResolver : IFeatureResolver
{
    private ReadOnlyMemory<byte> ionTypes;

    public List<string> RequiredSections { get; } = new List<string>
    {
        IonDataSectionName.IonType,
    };

    public void LoadChunk(IChunkState chunk)
    {
        ionTypes = chunk.ReadSectionData<byte>(IonDataSectionName.IonType);
    }

    public int GetFeature(int chunkIndex)
    {
        return ionTypes.Span[chunkIndex] != byte.MaxValue
            ? ionTypes.Span[chunkIndex]
            : Constants.InvalidFeature;

    }
}
