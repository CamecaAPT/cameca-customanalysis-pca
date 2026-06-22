using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.PcaLib.Interface;
using Cameca.CustomAnalysis.Utilities;
using System;
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

        var bytes = PackMatrixData(voxelIndices, voxelFeatureMatrix);
        long length = bytes.Length;
        ionData.DeleteSection(sectionName);
        ionData.AddSection(AddSectionContext.CreateUnrelated<byte>(sectionName, length));
        var extraData = new VoxelFeatureMatrixExtraData(gridParams, voxelIndices.Length);
        var jsonExtraData = JsonSerializer.Serialize(extraData);
        var jsonExtraDataBytes = Encoding.UTF8.GetBytes(jsonExtraData);
        ionData.Sections[sectionName].UpdateExtraData(jsonExtraDataBytes);
        ionData.WriteSection(sectionName, bytes);
    }

    public static bool TryReadFromIonDataSection(IIonData ionData, string sectionName, [NotNullWhen(true)]VoxelFeatureMatrixSectionData? data)
    {
        data = null;
        var extraData = JsonSerializer.Deserialize<VoxelFeatureMatrixExtraData>(ionData.Sections[sectionName].ExtraData);
        if (extraData is null)
        {
            return false;
        }
        var byteLength = ionData.Sections[sectionName].RecordCount;
        var voxelCount = extraData.NonEmptyVoxels;
        var indicesBytesLength = Marshal.SizeOf<int>() * voxelCount;
        var dataByteLength = (int)(byteLength - (ulong)indicesBytesLength);
        var dataFloatLength = dataByteLength / Marshal.SizeOf<float>();
        var featureCount = dataFloatLength / voxelCount;

        // Shouldn't have to iterated everything when only getting initial pointer.
        // Just getting the first should ensure the entire section is memory mapped
        var enumerator = ionData.CreateSectionDataEnumerator(sectionName);
        if (!enumerator.MoveNext())
        {
            return false;
        }
        var bytesData = enumerator.Current.ReadSectionData<byte>(sectionName);
        var pinnedData = bytesData.Pin();
        ReadOnlySpan<int> indiciesSpan;
        ReadOnlySpan<float> dataSpan;
        unsafe
        {
            var indicesPtr = pinnedData.Pointer;
            var dataPtr = (void*)((int*)pinnedData.Pointer + voxelCount);
            indiciesSpan = new ReadOnlySpan<int>(pinnedData.Pointer, voxelCount);
            dataSpan = new ReadOnlySpan<float>(dataPtr, dataFloatLength);
        }
        var voxelFeature = VoxelFeatureMatrix.FromData(dataSpan, voxelCount, featureCount, indiciesSpan);
        data = new VoxelFeatureMatrixSectionData(extraData.GridParameters, indiciesSpan.ToArray(), voxelFeature);
        return true;
    }

    private static byte[] PackMatrixData(ReadOnlyMemory<int> voxelIndices, VoxelFeatureMatrix voxelFeatureMatrix)
    {
        UnmanagedMemoryManager<float> mm;
        unsafe
        {
            float* nativePtr = (float*)voxelFeatureMatrix.DataPointer.ToPointer();
            int dataLength = voxelFeatureMatrix.DataLength;
            mm = new UnmanagedMemoryManager<float>(nativePtr, dataLength);
        }
        var indicesBytesLength = Marshal.SizeOf<int>() * voxelIndices.Length;
        var bytes = new byte[indicesBytesLength + Marshal.SizeOf<float>() * mm.Memory.Length];
        MemoryMarshal.AsBytes(voxelIndices.Span).CopyTo(bytes.AsSpan().Slice(0, indicesBytesLength));
        MemoryMarshal.AsBytes(mm.Memory.Span).CopyTo(bytes.AsSpan().Slice(indicesBytesLength));
        return bytes;
    }
}
