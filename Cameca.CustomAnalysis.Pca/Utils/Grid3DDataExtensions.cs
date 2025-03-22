using Cameca.CustomAnalysis.Interface;
using System.Numerics;

namespace Cameca.CustomAnalysis.Pca;

// This is generalizable enough to probably warrant extraction into Cameca.CustomAnalysis.Utilites for any extension to use
internal static class Grid3DDataExtensions
{
    public static Vector3 GetMinVector(this IGrid3DData gridData)
    {
        return new Vector3(
            (float)gridData.GridRange[0, 0],
            (float)gridData.GridRange[1, 0],
            (float)gridData.GridRange[2, 0]);
    }

    public static Vector3 GetVoxelSizeDimensions(this IGrid3DData gridData)
    {
        return new Vector3(
            (float)gridData.VoxelSize[0],
            (float)gridData.VoxelSize[1],
            (float)gridData.VoxelSize[2]);
    }
}
