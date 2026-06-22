using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using Prism.Events;

namespace Cameca.CustomAnalysis.Pca;

internal class VoxelizationMenuFactory : AnalysisMenuFactoryBase
{
    public VoxelizationMenuFactory(IEventAggregator eventAggregator)
        : base(eventAggregator)
    {
    }

    protected override INodeDisplayInfo DisplayInfo => VoxelizationAnalysis.DisplayInfo;
    protected override string NodeUniqueId => VoxelizationAnalysis.UniqueId;
    public override AnalysisMenuLocation Location { get; } = AnalysisMenuLocation.Analysis;
}
