using Cameca.CustomAnalysis.Utilities;

namespace Cameca.CustomAnalysis.Pca;

internal class ONMFViewModel : AnalysisViewModelBase<OrthNonNegMatrixFactorizationAnalysis>
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.ONMFViewModel";

    public ONMFViewModel(IAnalysisViewModelBaseServices services)
        : base(services)
    {
    }
}
