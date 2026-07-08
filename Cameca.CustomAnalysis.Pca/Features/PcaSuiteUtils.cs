using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.PcaLib.Interface;
using Cameca.CustomAnalysis.Utilities;
using Cameca.CustomAnalysis.Utilities.Segmentation;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Resources;

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

    public static VoxelFeatureMatrixSectionData GetVoxelFeatureMatrixSectionData(this IIonData ionData, string sectionName)
    {
        // This isn't the top level -- .Parent! is safe
        if (VoxelFeatureMatrixSerializer.ReadFromIonDataSection(ionData, sectionName) is not { } data)
        {
            throw new InvalidOperationException("Section does not define a VoxelFeatureMatrix");
        }
        return data;
    }
}
