using System.Collections.Generic;
using System.Windows.Navigation;
using Cameca.CustomAnalysis.Pca;
// OneDGridsResults represents the projection of the PCA Components results
// onto a single dimension's line
public class OneDGridsResults
{
    // key in these dictionaries is gridID
    public Dictionary<OneDGridID, OneDPeakProjection> OneDPeakProjections;
 
    public OneDGridsResults()
    {
        this.OneDPeakProjections = new Dictionary<OneDGridID, OneDPeakProjection>();
     }

    public void SetOneDPeakProjectionFor(OneDGridID key, OneDPeakProjection projection)
    {
        OneDPeakProjections[key] = projection;
    }
}
    
  