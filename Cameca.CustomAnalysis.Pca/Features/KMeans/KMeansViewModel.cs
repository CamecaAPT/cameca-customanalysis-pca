using Cameca.CustomAnalysis.Utilities;

namespace Cameca.CustomAnalysis.Pca;

internal class KMeansViewModel : AnalysisViewModelBase<KMeansAnalysis>
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.KMeansViewModel";

    public KMeansViewModel(IAnalysisViewModelBaseServices services)
        : base(services)
    {
    }
}
