using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.PcaLib.Interface;
using Cameca.CustomAnalysis.Utilities;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Cameca.CustomAnalysis.Pca;

internal static class IonAssignmentSerializer
{
    public static void WriteToIonDataSection(
        IIonData ionData,
        string sectionName,
        GridParameters gridParams,
        ReadOnlySpan<int> sparseIndexMap,
        ReadOnlySpan<byte> voxelAssignments)
    {
        var minVector = gridParams.GetMinVector();
        var voxelSize = gridParams.GetVoxelSizeDimensions();
        int xBinStride = gridParams.VoxelCount[0];
        int yBinStride = gridParams.VoxelCount[1];

        var sparseIndices = new Dictionary<int, int>(sparseIndexMap.Length);
        for (int i = 0; i < sparseIndexMap.Length; ++i)
        {
            sparseIndices[sparseIndexMap[i]] = i;
        }

        var binner = new PositionToVoxels(minVector, voxelSize, xBinStride, yBinStride);

        ionData.DeleteSection(sectionName);
        ionData.AddSection<byte>(sectionName);

        // Map all ions to their corresponding voxels, then then map that voxel to a phase ID
        foreach (var chunk in ionData.CreateSectionDataEnumerable(IonDataSectionName.Position, sectionName))
        {
            byte[] buffer = new byte[chunk.Length];
            var positionsMem = chunk.ReadSectionData<Vector3>(IonDataSectionName.Position);
            for (var i = 0; i < chunk.Length; i++)
            {
                Vector3 pos = positionsMem.Span[i];
                int voxel = binner.ToVoxel(pos);
                byte assignment;
                if (sparseIndices.TryGetValue(voxel, out int sparseIndex))
                {
                    assignment = voxelAssignments[sparseIndex];
                }
                else
                {
                    // TODO: This really should all map, as by definition any position should map
                    // to a non-empty, and therefore present, voxel in sparse representation
                    // Any missing here are likely to be floating point errors, and we should work on correcting
                    // Consider a logged warning here until fixed
                    assignment = byte.MaxValue;
                }
                buffer[i] = assignment;
            }
            chunk.WriteSectionData<byte>(sectionName, buffer);
        }
    }

    public static byte[]? ReadFromIonDataSection(IIonData ionData, string sectionName)
    {
        if (ionData.Sections.ContainsKey(sectionName))
        {
            return ionData.ReadSectionToArray<byte>(sectionName);
        }
        return null;
    }
}
