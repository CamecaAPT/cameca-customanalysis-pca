using Cameca.CustomAnalysis.PcaLib.Interface;
using System;
using System.Numerics;

namespace Cameca.CustomAnalysis.Pca.Utils;

internal static class PositionScores
{
    public static Vector4[] GetScoredPositions(GridParameters gridParams, int[] voxelIndices, float[] scores, float jitterStdDev = 0f)
    {
        int xVoxels = gridParams.VoxelCount[0];
        double xSize = gridParams.VoxelSize;
        double xStart = gridParams.GridStart[0] + (xSize / 2d);

        int yVoxels = gridParams.VoxelCount[1];
        double ySize = gridParams.VoxelSize;
        double yStart = gridParams.GridStart[1] + (ySize / 2d);

        int zVoxels = gridParams.VoxelCount[2];
        double zSize = gridParams.VoxelSize;
        double zStart = gridParams.GridStart[2] + (zSize / 2d);

        int indexer = 0;
        var positionsWithValues = new Vector4[voxelIndices.Length];

        int seed = 0;
        var r = new Random(seed);
        for (int z = 0; z < zVoxels; z++)
        {
            for (int y = 0; y < yVoxels; y++)
            {
                for (int x = 0; x < xVoxels; x++)
                {
                    int voxelIndex = (z * yVoxels * xVoxels) + (y * xVoxels) + x;
                    if (voxelIndices[indexer] == voxelIndex)
                    {
                        positionsWithValues[indexer] = new Vector4(
                            (float)(xStart + (xSize * x)) + r.NextSingleNormal(stdDev: jitterStdDev),
                            (float)(yStart + (ySize * y)) + r.NextSingleNormal(stdDev: jitterStdDev),
                            (float)(zStart + (zSize * z)) + r.NextSingleNormal(stdDev: jitterStdDev),
                            scores[indexer]);
                        indexer++;
                        if (indexer >= voxelIndices.Length)
                        {
                            break;
                        }
                    }
                }
                if (indexer >= voxelIndices.Length)
                {
                    break;
                }
            }
            if (indexer >= voxelIndices.Length)
            {
                break;
            }
        }

        return positionsWithValues;
    }

}
