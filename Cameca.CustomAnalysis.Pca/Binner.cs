using System.Numerics;

namespace Cameca.CustomAnalysis.Pca;

internal class Binner
{
    private readonly Vector3 minEdge;
    private readonly Vector3 voxelSize;
    private readonly int xBins;
    private readonly int yBins;
    private readonly Matrix4x4 transformation;

    public Binner(Vector3 minEdge, Vector3 voxelSize, int xBins, int yBins)
    {
        this.minEdge = minEdge;
        this.voxelSize = voxelSize;
        this.xBins = xBins;
        this.yBins = yBins;

        var scale = Matrix4x4.CreateScale(1f / voxelSize.X, 1f / voxelSize.Y, 1f / voxelSize.Z);
        transformation = scale
            * Matrix4x4.CreateTranslation(-(Vector3.Transform(minEdge, scale)));
    }

    public int ToBin(Vector3 position)
    {
        var normalized = Vector3.Transform(position, transformation);
        int xFloor = (int)normalized.X;
        int yFloor = (int)normalized.Y;
        int zFloor = (int)normalized.Z;
        return xFloor + (yFloor * xBins) + (zFloor * xBins * yBins);
    }
}
