using Cameca.CustomAnalysis.Utilities;

namespace Cameca.CustomAnalysis.Pca;

internal class GMMViewModel : AnalysisViewModelBase<GaussianMixtureModelAnalysis>
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.GMMViewModel";

    public GMMViewModel(IAnalysisViewModelBaseServices services)
        : base(services)
    {
    }
}
