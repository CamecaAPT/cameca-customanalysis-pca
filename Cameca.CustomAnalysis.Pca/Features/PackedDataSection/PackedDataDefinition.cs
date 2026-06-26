using System;
using System.Collections.Generic;
using System.Linq;

namespace Cameca.CustomAnalysis.Pca;

internal class PackedDataDefinition<TExtraData> where TExtraData : class
{
    public TExtraData ExtraData { get; }
    public OrderedDictionary<string, IPackedData> PackedData { get; }

    public PackedDataDefinition(TExtraData extraData, IEnumerable<KeyValuePair<string, IPackedData>> packedData)
    {
        this.ExtraData = extraData;
        this.PackedData = new OrderedDictionary<string, IPackedData>(packedData);
    }

    public int RequiredDataSize => PackedData.Select(x => x.Value).Sum(x => x.LengthBytes);
}

internal class PackedDataDefinitionBuilder<TExtraData> where TExtraData : class
{
    private readonly TExtraData extraData;
    private readonly List<KeyValuePair<string, IPackedData>> packedData;

    public PackedDataDefinitionBuilder(TExtraData extraData)
    {
        this.extraData = extraData;
        packedData = new();
    }

    public PackedDataDefinitionBuilder<TExtraData> AddPackedData<T>(string key, ReadOnlyMemory<T> data, int[] shape, StorageOrder storageOrder = default)
        where T : unmanaged
    {
        if (packedData.Any(x => x.Key == key))
        {
            throw new ArgumentException($"Data key \"{key}\" as already added. All keys must be unique.");
        }
        var dtype = TypeMap.DType<T>();
        packedData.Add(new KeyValuePair<string, IPackedData>(key, new WritePackedData<T>(dtype, data, shape, storageOrder)));
        return this;
    }

    public PackedDataDefinition<TExtraData> Build()
    {
        return new PackedDataDefinition<TExtraData>(extraData, packedData);
    }
}
