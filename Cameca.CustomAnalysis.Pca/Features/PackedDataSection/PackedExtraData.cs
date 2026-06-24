using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cameca.CustomAnalysis.Pca;

public class PackedExtraData<TExtraData> where TExtraData : class
{
    [JsonPropertyName("manifest")]
    public Dictionary<string, PackedDataInfo> Manifest { get; }

    [JsonPropertyName("extra_data")]
    public TExtraData? ExtraData { get; }

    [JsonConstructor]
    public PackedExtraData(
        Dictionary<string, PackedDataInfo> manifest,
        TExtraData? extraData = null)
    {
        Manifest = manifest;
        ExtraData = extraData;
    }
}