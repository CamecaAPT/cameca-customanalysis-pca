using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using Prism.Events;

namespace Cameca.CustomAnalysis.Pca;

internal class PcaNodeMenuFactory : AnalysisMenuFactoryBase
{
    public PcaNodeMenuFactory(IEventAggregator eventAggregator)
        : base(eventAggregator)
    {
    }

    protected override INodeDisplayInfo DisplayInfo => PcaNode.DisplayInfo;
    protected override string NodeUniqueId => PcaNode.UniqueId;
    public override AnalysisMenuLocation Location { get; } = AnalysisMenuLocation.Analysis;
}