using Cameca.CustomAnalysis.Interface;
using System.Collections.Generic;

namespace Cameca.CustomAnalysis.Pca;

internal interface IFeatureResolver
{
    List<string> RequiredSections { get; }
    void LoadChunk(IChunkState chunk);
    int GetFeature(int chunkIndex);
}
