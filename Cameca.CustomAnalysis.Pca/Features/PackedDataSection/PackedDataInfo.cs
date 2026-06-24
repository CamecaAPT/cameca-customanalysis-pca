using System.Text.Json.Serialization;

namespace Cameca.CustomAnalysis.Pca;


[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StorageOrder
{
    C = default,  // C-style row-major ordering (default [0] - omitted when serializing)
    F = 1,  // Fortran-style column-major ordering
}

public class PackedDataInfo
{
    [JsonPropertyName("dtype")]
    public string DType { get; }

    [JsonPropertyName("shape")]
    public int[] Shape { get; }

    [JsonPropertyName("data_offsets")]
    public int[] DataOffsets { get; }

    [JsonPropertyName("order")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public StorageOrder Order { get; }

    [JsonConstructor]
    public PackedDataInfo(
        string dtype,
        int[] shape,
        int[] dataOffsets,
        StorageOrder order = default)
    {
        DType = dtype;
        Shape = shape;
        DataOffsets = dataOffsets;
        Order = order;
    }
}