using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.PcaLib.Interface;
using System;
using System.Windows;

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
    public static VoxelFeatureMatrixSectionData? GetNullableVoxelFeatureMatrixSectionData(this IIonData ionData, string sectionName, Func<bool> getErrorState, Action<bool> setErrorState)
    {
        var sectionData = VoxelFeatureMatrixSerializer.ReadFromIonDataSection(ionData, sectionName);
        // If could not resolve the VoxelFeatureMatrixSectionData, then warn the user, set error state to true, and return null.
        // Retrieve existing error state to avoid showing the message box multiple times.
        if (sectionData is null && !getErrorState())
        {
            MessageBox.Show("There are no features in the grid on which to perform the analysis.", "PCA Error", MessageBoxButton.OK, MessageBoxImage.Error);
            setErrorState(true);
            return null;
        }
        setErrorState(sectionData is null);
        return sectionData;
    }
}
