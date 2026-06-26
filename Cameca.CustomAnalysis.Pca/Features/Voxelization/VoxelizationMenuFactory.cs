using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using Prism.Events;

namespace Cameca.CustomAnalysis.Pca;

internal class VoxelizationMenuFactory : ConditionalMenuFactoryBase
{
    public VoxelizationMenuFactory(IEventAggregator eventAggregator, INodeInfoProvider nodeInfoProvider) : base(eventAggregator, nodeInfoProvider)
    {
    }

    protected override INodeDisplayInfo DisplayInfo => VoxelizationAnalysis.DisplayInfo;
    protected override string NodeUniqueId => VoxelizationAnalysis.UniqueId;
    public override AnalysisMenuLocation Location { get; } = AnalysisMenuLocation.Analysis;

    public override string[]? DisallowedTypes { get; } = new string[]
    {
        VoxelizationAnalysis.UniqueId,
    };
}
