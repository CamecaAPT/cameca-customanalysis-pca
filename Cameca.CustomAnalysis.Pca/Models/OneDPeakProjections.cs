
using Cameca.CustomAnalysis.Pca.VoxelLogic;

namespace Cameca.CustomAnalysis.Pca.Models;

public sealed class OneDPeakProjection
{
    public DensityLine densityLine;

    public OneDPeakProjection(DensityLine dl)
    {
        this.densityLine = dl;
    }

  
}

