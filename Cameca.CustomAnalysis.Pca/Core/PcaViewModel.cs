using Cameca.CustomAnalysis.Utilities;

namespace Cameca.CustomAnalysis.Pca;

internal class PcaViewModel : AnalysisViewModelBase<PrincipalComponentAnalysis>
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.PcaViewModel";

    public PcaViewModel(IAnalysisViewModelBaseServices services)
        : base(services)
    {
    }
}
