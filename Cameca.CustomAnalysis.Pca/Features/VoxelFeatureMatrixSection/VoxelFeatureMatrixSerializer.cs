using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.PcaLib.Interface;
using Cameca.CustomAnalysis.Utilities;
using System;
using System.Buffers;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Cameca.CustomAnalysis.Pca;

public record VoxelFeatureMatrixSectionData(VoxelFeatureMatrixExtraData ExtraData, VoxelFeatureMatrix VoxelFeatureMatrix, VoxelFeatureMatrix? LoadingsMatrix = null);

internal static class VoxelFeatureMatrixSerializer
{
    public static void WriteToIonDataSection(IIonData ionData, string sectionName, VoxelFeatureMatrixSectionData data)
    {
        var voxelFeatureMatrix = data.VoxelFeatureMatrix;
        var voxelIndices = voxelFeatureMatrix.GetVoxelIndices();
        int rows = voxelFeatureMatrix.VoxelCount;
        int cols = voxelFeatureMatrix.FeatureCount;
        var matrixData = GetVoxelFeatureMatrixData(voxelFeatureMatrix);

        var packedData = new PackedDataDefinitionBuilder<VoxelFeatureMatrixExtraData>(data.ExtraData)
            .AddPackedData<int>("voxel_indices", voxelIndices, new int[] { voxelIndices.Length })
            .AddPackedData<float>("matrix", matrixData, new int[] { rows, cols }, StorageOrder.F)
            .Build();

        PackedDataSerializer.Write(ionData, sectionName, packedData);
    }

    public static VoxelFeatureMatrixSectionData? ReadFromIonDataSection(IIonData ionData, string sectionName)
    {
        if (PackedDataSerializer.Read<VoxelFeatureMatrixExtraData>(ionData, sectionName) is not { } definition
            || !(definition.PackedData.TryGetValue("voxel_indices", out var indicesInfo)
                 && indicesInfo is { DType: "<i4", Shape: { Length: 1 }, Order: StorageOrder.C })
            || !(definition.PackedData.TryGetValue("matrix", out var matrixInfo)
                 && matrixInfo is { DType: "<f4", Shape: { Length: 2 }, Order: StorageOrder.F }))
        {
            return null;
        }

        var indicesData = indicesInfo.GetDataAsType<int>();
        var matrixData = matrixInfo.GetDataAsType<float>();
        var matrix = VoxelFeatureMatrix.FromData(matrixData, matrixInfo.Shape[0], matrixInfo.Shape[1], indicesData);

        if (definition.PackedData.TryGetValue("loadings", out var loadingsInfo))
        {
            if (loadingsInfo is not { DType: "<f4", Shape: { Length: 2 }, Order: StorageOrder.F })
            {
                throw new InvalidOperationException($"Expected 'loadings' to be a 2D float32 array in Fortran order, but got DType: {loadingsInfo.DType}, Shape: [{string.Join(", ", loadingsInfo.Shape)}], Order: {loadingsInfo.Order}");
            }
            var loadingsData = loadingsInfo.GetDataAsType<float>();
            var loadings = VoxelFeatureMatrix.FromData(loadingsData, loadingsInfo.Shape[0], loadingsInfo.Shape[1], indicesData);
            return new VoxelFeatureMatrixSectionData(definition.ExtraData, matrix, loadings);
        }

        return new VoxelFeatureMatrixSectionData(definition.ExtraData, matrix);
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

    private unsafe static ReadOnlyMemory<float> GetVoxelFeatureMatrixData(VoxelFeatureMatrix voxelFeatureMatrix)
    {
        float* nativePtr = (float*)voxelFeatureMatrix.DataPointer.ToPointer();
        int dataLength = voxelFeatureMatrix.DataLength;
        return (new UnmanagedMemoryManager<float>(nativePtr, dataLength)).Memory;
    }
}
