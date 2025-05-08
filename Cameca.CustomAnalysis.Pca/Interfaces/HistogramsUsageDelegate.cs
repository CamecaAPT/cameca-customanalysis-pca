using Cameca.CustomAnalysis.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Cameca.CustomAnalysis.Pca;


public interface IHistogramsUsageDelegate
{
    void UseHistogramForPca(string histogramID, bool useIt);
    bool UsesHistogramForPca(string histogramID);
}

public class DoNothingHistogramsUsageDelegate : IHistogramsUsageDelegate
{
    public DoNothingHistogramsUsageDelegate()
    {
    }

    public void UseHistogramForPca(string gridID, bool useIt)
    {

    }

    public bool UsesHistogramForPca(string gridID)
    {
        return true;
    }
}