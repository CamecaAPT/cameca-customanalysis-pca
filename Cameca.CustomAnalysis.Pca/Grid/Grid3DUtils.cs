using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.PcaLib.Interface;
using System;

namespace Cameca.CustomAnalysis.Pca;

internal class Grid3DUtils
{
    /// <summary>
    /// Calculate voxel counts and grid ranges 
    /// </summary>
    /// <remarks>
    /// AP Suite grid requirements: match this logic for consistency in extension + host application 3D grid generation
    /// <br />
    /// - Add an edge buffer to allow for less bounds checking in kernal distribution and exterior suface generation on fill-missing
    /// <br />
    /// - Odd number of voxels for delocalization kernal that has an odd number of voxels to be able to be used in place
    /// </remarks>
    /// <param name="roiExtents"></param>
    /// <param name="voxelSize"></param>
    /// <param name="edgeBuffer"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static GridParameters CreateGridParameters(Extents roiExtents, double voxelSize, double edgeBuffer = 1.5d)
    {
        if (edgeBuffer < 0d) throw new ArgumentOutOfRangeException(nameof(edgeBuffer), edgeBuffer, $"{nameof(edgeBuffer)} must be non-negative value");

        double[][] extents = new double[][]
        {
        new double[] { roiExtents.Min.X, roiExtents.Max.X },
        new double[] { roiExtents.Min.Y, roiExtents.Max.Y },
        new double[] { roiExtents.Min.Z, roiExtents.Max.Z },
        };
        double[] gridStart = new double[3];
        int[] voxelCount = new int[3];

        // Calculate voxel counts and grid ranges
        // AP Suite grid requirements: match this logic for consistency in extension + host application 3D grid generation
        // - Add an edge buffer to allow for less bounds checking in kernal distribution and exterior suface generation on fill-missing
        // - Odd number of voxels for delocalization kernal that has an odd number of voxels to be able to be used in place
        for (int i = 0; i < 3; i++)
        {
            double delta = extents[i][1] - extents[i][0];
            double center = extents[i][1] - delta / 2d;
            double halfCount = Math.Ceiling((delta / 2d) / voxelSize) + edgeBuffer;

            gridStart[i] = center - halfCount * voxelSize;
            voxelCount[i] = (int)(2.0 * halfCount + 0.5);
        }
        return new GridParameters(gridStart, voxelSize, voxelCount);
    }
}

