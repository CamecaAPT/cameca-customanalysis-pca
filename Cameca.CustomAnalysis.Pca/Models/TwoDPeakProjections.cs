
using Cameca.CustomAnalysis.Pca.VoxelLogic;
namespace Cameca.CustomAnalysis.Pca.Models;

public sealed class TwoDPeakProjection
{
    public DensityPlane densityPlane;

    public TwoDPeakProjection(DensityPlane dp)
    {
        this.densityPlane = dp;
    }
}

