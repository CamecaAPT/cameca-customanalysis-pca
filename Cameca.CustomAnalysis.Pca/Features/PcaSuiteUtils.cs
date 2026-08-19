using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.PcaLib.Interface;
using System;

namespace Cameca.CustomAnalysis.Pca;

internal static class PcaSuiteUtils
{
    public unsafe static ReadOnlyMemory<float> GetFeatureData(this VoxelFeatureMatrix voxelFeatureMatrix, int featureIndex)
    {
        if (voxelFeatureMatrix.FeatureCount <= featureIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(featureIndex), "Feature index is out of range.");
        }
        int dataLength = voxelFeatureMatrix.VoxelCount;
        float* nativePtr = ((float*)voxelFeatureMatrix.DataPointer.ToPointer()) + (featureIndex * dataLength);
        return (new UnmanagedMemoryManager<float>(nativePtr, dataLength)).Memory;
    }

    // Callers to GetNullableVoxelFeatureMatrixSectionData must handle the case where null is returned.
    public static VoxelFeatureMatrixSectionData? GetNullableVoxelFeatureMatrixSectionData(this IIonData ionData, string sectionName)
    {
        // This isn't the top level -- .Parent! is safe
        return VoxelFeatureMatrixSerializer.ReadFromIonDataSection(ionData, sectionName);
    }
}
