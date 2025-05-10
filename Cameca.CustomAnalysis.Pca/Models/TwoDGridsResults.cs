using System.Collections.Generic;
using Cameca.CustomAnalysis.Pca;
// TwoDGridsResults represents the projection of the PCA Components results
// onto two dimensional drids for different pairs of axes
public class TwoDGridsResults
{
    // key in these dictionaries is gridID
    public Dictionary<TwoDGridID, TwoDPeakProjection> TwoDPeakProjections;
 
    public TwoDGridsResults()
    {
        this.TwoDPeakProjections = new Dictionary<TwoDGridID, TwoDPeakProjection>();
     }

    public void SetTwoDPeakProjectionFor(TwoDGridID key, TwoDPeakProjection projection)
    {
        TwoDPeakProjections[key] = projection;
    }

}
    
  