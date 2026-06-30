using Cameca.CustomAnalysis.Interface;
using Prism.Events;

namespace Cameca.CustomAnalysis.Pca;

internal class PcaNodeMenuFactory : ConditionalMenuFactoryBase
{
    public PcaNodeMenuFactory(IEventAggregator eventAggregator, INodeInfoProvider nodeInfoProvider)
        : base(eventAggregator, nodeInfoProvider)
    {
    }

    protected override INodeDisplayInfo DisplayInfo => PrincipalComponentAnalysis.DisplayInfo;
    protected override string NodeUniqueId => PrincipalComponentAnalysis.UniqueId;
    public override AnalysisMenuLocation Location { get; } = AnalysisMenuLocation.Analysis;

    public override string[]? AllowedTypes { get; } = new string[]
    {
        VoxelizationAnalysis.UniqueId,
    };
}
