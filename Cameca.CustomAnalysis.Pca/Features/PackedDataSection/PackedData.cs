using System;
using System.Buffers;
using System.Linq;
using System.Runtime.InteropServices;

namespace Cameca.CustomAnalysis.Pca;

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

internal record WritePackedData<T>(string DType, ReadOnlyMemory<T> Data, int[] Shape, StorageOrder Order = default) : IPackedData
    where T : unmanaged
{
    public int LengthBytes => Data.Length * Marshal.SizeOf(TypeMap.SystemType(DType));
    public ReadOnlySpan<TTarget> GetDataAsType<TTarget>() where TTarget : unmanaged
    {
        return MemoryMarshal.Cast<T, TTarget>(Data.Span);
    }
}