using System.Collections.Generic;
using System.Windows.Documents;

namespace Cameca.CustomAnalysis.Pca;

public sealed class TwoDPeakProjection
{
    public DensityPlane densityPlane;

    public TwoDPeakProjection(DensityPlane dp)
    {
        this.densityPlane = dp;
    }
}

