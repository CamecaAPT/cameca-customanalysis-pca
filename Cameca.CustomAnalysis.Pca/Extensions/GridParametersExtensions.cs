using Cameca.CustomAnalysis.PcaLib.Interface;
using System.Numerics;

namespace Cameca.CustomAnalysis.Pca;

internal static class GridParametersExtensions
{
    public static Vector3 GetMinVector(this GridParameters gridParams)
    {
        return new Vector3(
            (float)gridParams.GridStart[0],
            (float)gridParams.GridStart[1],
            (float)gridParams.GridStart[2]);
    }

    public static Vector3 GetVoxelSizeDimensions(this GridParameters gridParams)
    {
        return new Vector3(
            (float)gridParams.VoxelSize,
            (float)gridParams.VoxelSize,
            (float)gridParams.VoxelSize);
    }
}
