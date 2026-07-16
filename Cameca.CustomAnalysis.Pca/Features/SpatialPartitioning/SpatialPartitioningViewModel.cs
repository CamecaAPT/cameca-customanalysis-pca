using Cameca.CustomAnalysis.Utilities;

namespace Cameca.CustomAnalysis.Pca;

internal class SpatialPartitioningViewModel : AnalysisViewModelBase<SpatialPartitioningAnalysis>
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.SpatialPartitioningViewModel";

    public SpatialPartitioningViewModel(IAnalysisViewModelBaseServices services)
        : base(services)
    {
    }
}
