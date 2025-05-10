using Cameca.CustomAnalysis.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Cameca.CustomAnalysis.Pca;

/// IHistogramsUsageDelegate is used to communicate changes from the UI back to the model about
/// which oneDGrids (PCA histograms) to use in the PCA Phase determination process
/// The main class can implement this protocol and then inject itself as a delegate for
/// the UI
public interface IHistogramsUsageDelegate
{
    void UseHistogramForPca(string histogramID, bool useIt);
    bool UsesHistogramForPca(string histogramID);
}

/// DoNothingHistogramsUsageDelegate is the default implementation of the delegate
/// so that the UI can be initialized with a default (or tested separately from the modedl)
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