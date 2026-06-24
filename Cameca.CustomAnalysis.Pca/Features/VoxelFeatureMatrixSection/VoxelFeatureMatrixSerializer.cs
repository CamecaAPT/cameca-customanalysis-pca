using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.PcaLib.Interface;
using Cameca.CustomAnalysis.Utilities;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Cameca.CustomAnalysis.Pca;

public record VoxelFeatureMatrixSectionData(GridParameters GridParameters, ReadOnlyMemory<int> VoxelIndices, VoxelFeatureMatrix VoxelFeatureMatrix);

internal static class VoxelFeatureMatrixSerializer
{
    public static void WriteToIonDataSection(IIonData ionData, string sectionName, GridParameters gridParams, ReadOnlyMemory<int> voxelIndices, VoxelFeatureMatrix voxelFeatureMatrix)
    {
        // Pack as bytes in order:
        // - Int array of non-empty voxel indices
        // - 2D float array (column major) or non-empty voxels x features

        byte[] indicesBytes = PackVoxelIndices(voxelIndices);
        byte[] matrixBytes = PackVoxelFeatureMatrix(voxelFeatureMatrix);
        long length = indicesBytes.Length + matrixBytes.Length;
        ionData.DeleteSection(sectionName);
        ionData.AddSection(AddSectionContext.CreateUnrelated<byte>(sectionName, length));
        long voxelIndicesByteLength = voxelIndices.Length * Marshal.SizeOf<int>();
        long matrixByteLength = matrixBytes.Length;
        int rows = voxelFeatureMatrix.VoxelCount;
        int cols = voxelFeatureMatrix.FeatureCount;
        var dataManifest = new Dictionary<string, PackedDataInfo>
        {
            ["voxel_indices"] = new PackedDataInfo("i32", new long[] { voxelIndices.Length }, new long[] { 0, voxelIndicesByteLength }),
            ["matrix"] = new PackedDataInfo("f32", new long[] { rows, cols }, new long[] { voxelIndicesByteLength, voxelIndicesByteLength + matrixByteLength }, StorageOrder.F),
        };
        var extraData = new VoxelFeatureMatrixExtraData(gridParams);
        var packedExtraData = new PackedExtraData<VoxelFeatureMatrixExtraData>(dataManifest, extraData);
        var jsonExtraData = JsonSerializer.Serialize(packedExtraData);
        var jsonExtraDataBytes = Encoding.UTF8.GetBytes(jsonExtraData);
        ionData.Sections[sectionName].UpdateExtraData(jsonExtraDataBytes);
        byte[] allBytes = new byte[length];
        indicesBytes.AsSpan().CopyTo(allBytes.AsSpan().Slice(0, (int)voxelIndicesByteLength));
        matrixBytes.AsSpan().CopyTo(allBytes.AsSpan().Slice((int)voxelIndicesByteLength));
        ionData.WriteSection(sectionName, allBytes);
    }

    private static ReadOnlySpan<T> ReadPackedData<T>(MemoryHandle handle, PackedDataInfo dataInfo) where T : unmanaged
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

    public static bool TryReadFromIonDataSection(IIonData ionData, string sectionName, [NotNullWhen(true)]VoxelFeatureMatrixSectionData? data)
    {
        data = null;
        var packedExtraData = JsonSerializer.Deserialize<PackedExtraData<VoxelFeatureMatrixExtraData>>(ionData.Sections[sectionName].ExtraData);
        // Extract extra data, and parse and validate the expected data manifest
        if (packedExtraData is not { Manifest: { } manifest, ExtraData: { } extraData }
            || !(manifest.TryGetValue("voxel_indices", out var voxelIndicesInfo) && voxelIndicesInfo is { Shape: { Length: 1 }, DType: "i32", Order: StorageOrder.C })
            || !(manifest.TryGetValue("matrix", out var matrixInfo) && matrixInfo is { Shape: { Length: 2 }, DType: "f32", Order: StorageOrder.F }))
        {
            return false;
        }

        // Shouldn't have to iterated everything when only getting initial pointer.
        // Just getting the first should ensure the entire section is memory mapped
        var enumerator = ionData.CreateSectionDataEnumerator(sectionName);
        if (!enumerator.MoveNext())
        {
            return false;
        }
        var bytesData = enumerator.Current.ReadSectionData<byte>(sectionName);
        var pinnedData = bytesData.Pin();

        ReadOnlySpan<int> indiciesSpan = ReadPackedData<int>(pinnedData, voxelIndicesInfo);
        ReadOnlySpan<float> dataSpan = ReadPackedData<float>(pinnedData, matrixInfo);
        var voxelFeature = VoxelFeatureMatrix.FromData(dataSpan, (int)matrixInfo.Shape[0], (int)matrixInfo.Shape[1], indiciesSpan);
        data = new VoxelFeatureMatrixSectionData(extraData.GridParameters, indiciesSpan.ToArray(), voxelFeature);
        return true;
    }

    private static byte[] PackVoxelIndices(ReadOnlyMemory<int> voxelIndices)
    {
        var bytes = new byte[voxelIndices.Length * Marshal.SizeOf<int>()];
        MemoryMarshal.AsBytes(voxelIndices.Span).CopyTo(bytes.AsSpan());
        return bytes;
    }

    private static byte[] PackVoxelFeatureMatrix(VoxelFeatureMatrix voxelFeatureMatrix)
    {
        UnmanagedMemoryManager<float> mm;
        unsafe
        {
            float* nativePtr = (float*)voxelFeatureMatrix.DataPointer.ToPointer();
            int dataLength = voxelFeatureMatrix.DataLength;
            mm = new UnmanagedMemoryManager<float>(nativePtr, dataLength);
        }
        var bytes = new byte[Marshal.SizeOf<float>() * mm.Memory.Length];
        MemoryMarshal.AsBytes(mm.Memory.Span).CopyTo(bytes.AsSpan());
        return bytes;
    }
}
