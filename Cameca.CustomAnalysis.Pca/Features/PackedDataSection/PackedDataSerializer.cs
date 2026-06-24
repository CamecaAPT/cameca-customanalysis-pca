using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Media.Animation;

namespace Cameca.CustomAnalysis.Pca;

internal static class PackedDataSerializer
{
    public static void Write<TExtraData>(IIonData ionData, string sectionName, PackedDataDefinition<TExtraData> packedData) where TExtraData : class
    {
        var (manifest, buffer) = Build(packedData);
        ionData.DeleteSection(sectionName);
        ionData.AddSection(AddSectionContext.CreateUnrelated<byte>(sectionName, buffer.Length));
        var extraData = new PackedExtraData<TExtraData>(manifest, packedData.ExtraData);
        var jsonExtraData = JsonSerializer.Serialize(extraData);
        var jsonExtraDataBytes = Encoding.UTF8.GetBytes(jsonExtraData);
        ionData.Sections[sectionName].UpdateExtraData(jsonExtraDataBytes);
        ionData.WriteSection(sectionName, buffer);
    }

    public static PackedDataDefinition<TExtraData>? Read<TExtraData>(IIonData ionData, string sectionName) where TExtraData : class
    {
        if (!TryGetPinnedHandle(ionData, sectionName, out var handle))
        {
            return null;
        }

        var packedExtraData = JsonSerializer.Deserialize<PackedExtraData<TExtraData>>(ionData.Sections[sectionName].ExtraData);
        // Extract the manifest and extra data
        if (packedExtraData is not { Manifest: { } manifest, ExtraData: TExtraData })
        {
            return null;
        }
        var extraData = packedExtraData.ExtraData;

        // Parse out data
        var packedData = new Dictionary<string, IPackedData>();
        foreach (var (key, info) in packedExtraData.Manifest)
        {
            packedData[key] = new ReadPackedData(info, handle);
        }

        return new PackedDataDefinition<TExtraData>(extraData, packedData);
    }

    private static bool TryGetPinnedHandle(IIonData ionData, string sectionName, out MemoryHandle handle)
    {
        // Shouldn't have to iterated everything when only getting initial pointer.
        // Just getting the first should ensure the entire section is memory mapped
        var enumerator = ionData.CreateSectionDataEnumerator(sectionName);
        if (!enumerator.MoveNext())
        {
            handle = default;
            return false;
        }
        var bytesData = enumerator.Current.ReadSectionData<byte>(sectionName);
        handle = bytesData.Pin();
        return true;
    }

    /// <summary>
    /// Output both the header (extra data) and the buffer at the same time
    /// </summary>
    /// <remarks>
    /// Data is written to an allocated input buffer to avoid unnecessary extra copies.
    /// Extra data is returned at the same time to ensure the manifest in the packed header matches the output data.
    /// </remarks>
    /// <exception cref="ArgumentException"></exception>
    private static (Dictionary<string, PackedDataInfo> Manifest, ReadOnlyMemory<byte> Data) Build<TExtraData>(PackedDataDefinition<TExtraData> definition) where TExtraData : class
    {
        // Resolve to the current required size
        Memory<byte> buffer = new byte[definition.RequiredDataSize];

        var dataManifest = new Dictionary<string, PackedDataInfo>();
        int offset = 0;
        foreach (var (key, item) in definition.PackedData)
        {
            int length = item.LengthBytes;
            dataManifest[key] = new PackedDataInfo(item.DType, item.Shape, new int[] { offset, offset + length }, item.Order);
            var bytes = item.GetDataAsType<byte>();
            bytes.CopyTo(buffer.Span.Slice(offset, length));
            offset += length;
        }
        return (dataManifest, buffer);
    }
}


//internal class PackedSection<TExtraData> where TExtraData : class
//{
//    public PackedExtraData<TExtraData> ExtraData { get; init; }
//    public ReadOnlyMemory<T> GetData<T>(string key) where T : unmanaged
//    {

//    }
//}


internal interface IPackedData
{
    string DType { get; }
    int[] Shape { get; }
    StorageOrder Order { get; }
    int LengthBytes { get; }
    ReadOnlySpan<TTarget> GetDataAsType<TTarget>() where TTarget : unmanaged;
}

internal class ReadPackedData : IPackedData
{
    private readonly PackedDataInfo dataInfo;
    private readonly MemoryHandle pinnedData;

    public ReadPackedData(PackedDataInfo dataInfo, MemoryHandle pinnedData)
    {
        this.dataInfo = dataInfo;
        this.pinnedData = pinnedData;
    }

    public string DType => dataInfo.DType;

    public int[] Shape => dataInfo.Shape;

    public StorageOrder Order => dataInfo.Order;

    public int LengthBytes => dataInfo.Shape.Aggregate(1, (acc, val) => acc * val) * Marshal.SizeOf(TypeMap.SystemType(DType));

    public ReadOnlySpan<TTarget> GetDataAsType<TTarget>() where TTarget : unmanaged
    {
        return Read<TTarget>(pinnedData, dataInfo);
    }

    private static ReadOnlySpan<T> Read<T>(MemoryHandle handle, PackedDataInfo dataInfo) where T : unmanaged
    {
        var offsets = dataInfo.DataOffsets;
        unsafe
        {
            var indicesPtr = handle.Pointer;
            void* start = (byte*)handle.Pointer + offsets[0];
            int typedLength = (int)(offsets[1] - offsets[0]) / Marshal.SizeOf<T>();
            return new ReadOnlySpan<T>(start, typedLength);
        }
    }
}

internal record PackedData<T>(string DType, ReadOnlyMemory<T> Data, int[] Shape, StorageOrder Order = default) : IPackedData
    where T : unmanaged
{
    public int LengthBytes => Data.Length * Marshal.SizeOf(TypeMap.SystemType(DType));
    public ReadOnlySpan<TTarget> GetDataAsType<TTarget>() where TTarget : unmanaged
    {
        return MemoryMarshal.Cast<T, TTarget>(Data.Span);
    }
}

internal class PackedDataBuilder<TExtraData> where TExtraData : class
{
    private readonly TExtraData extraData;
    private readonly List<KeyValuePair<string, IPackedData>> packedData;

    public PackedDataBuilder(TExtraData extraData)
    {
        this.extraData = extraData;
        packedData = new();
    }

    public PackedDataBuilder<TExtraData> AddPackedData<T>(string key, ReadOnlyMemory<T> data, int[] shape, StorageOrder storageOrder = default)
        where T : unmanaged
    {
        if (packedData.Any(x => x.Key == key))
        {
            throw new ArgumentException($"Data key \"{key}\" as already added. All keys must be unique.");
        }
        var dtype = TypeMap.DType<T>();
        packedData.Add(new KeyValuePair<string, IPackedData>(key, new PackedData<T>(dtype, data, shape, storageOrder)));
        return this;
    }

    public PackedDataDefinition<TExtraData> Build()
    {
        return new PackedDataDefinition<TExtraData>(extraData, packedData);
    }
}

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

    ///// <summary>
    ///// Output both the header (extra data) and the buffer at the same time
    ///// </summary>
    ///// <remarks>
    ///// Data is written to an allocated input buffer to avoid unnecessary extra copies.
    ///// Extra data is returned at the same time to ensure the manifest in the packed header matches the output data.
    ///// </remarks>
    ///// <exception cref="ArgumentException"></exception>
    //public PackedExtraData<TExtraData> Build(Span<byte> buffer)
    //{
    //    // Resolve to the current required size
    //    long requiredSize = RequiredDataSize;
    //    if (buffer.Length != requiredSize)
    //    {
    //        throw new ArgumentException($"Invalid buffer size: Buffer must be {requiredSize} bytes. Use {GetType().Name}.{nameof(RequiredDataSize)} to allocate correct target size.");
    //    }

    //    var dataManifest = new Dictionary<string, PackedDataInfo>();
    //    int offset = 0;
    //    foreach (var (key, item) in packedData)
    //    {
    //        int length = item.LengthBytes;
    //        dataManifest[key] = new PackedDataInfo(TypeMap.DType(item.Type), item.Shape, new int[] { offset, offset + length }, item.Order);
    //        var bytes = item.GetDataAsType<byte>();
    //        bytes.CopyTo(buffer.Slice(offset, length));
    //        offset += length;
    //    }

    //    // Return extra data to ensure the manifest is constructed at the same time as the buffer copy so they are guarenteed to match
    //    return new PackedExtraData<TExtraData>(dataManifest, ExtraData);
    //}
}