using Cameca.CustomAnalysis.Utilities;

namespace Cameca.CustomAnalysis.Pca;

internal class MnMMViewModel : AnalysisViewModelBase<MnMMAnalysis>
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.MnMMViewModel";

    public MnMMViewModel(IAnalysisViewModelBaseServices services)
        : base(services)
    {
    }
}
