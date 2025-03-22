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

    protected override INodeDisplayInfo DisplayInfo => PrincipalComponentAnalysis.DisplayInfo;
    protected override string NodeUniqueId => PrincipalComponentAnalysis.UniqueId;
    public override AnalysisMenuLocation Location { get; } = AnalysisMenuLocation.Analysis;
}