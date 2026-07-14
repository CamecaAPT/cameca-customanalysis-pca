using Cameca.CustomAnalysis.Utilities;

namespace Cameca.CustomAnalysis.Pca;

internal class NegMnMMViewModel : AnalysisViewModelBase<NegMnMMAnalysis>
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.NegMnMMViewModel";

    public NegMnMMViewModel(IAnalysisViewModelBaseServices services)
        : base(services)
    {
    }
}
