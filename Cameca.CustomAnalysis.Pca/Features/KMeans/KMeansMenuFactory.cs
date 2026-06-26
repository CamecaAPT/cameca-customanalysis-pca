using Cameca.CustomAnalysis.Interface;
using CommunityToolkit.Mvvm.Input;
using Prism.Events;
using System.Windows.Controls;

namespace Cameca.CustomAnalysis.Pca;

internal class KMeansMenuFactory : ConditionalMenuFactoryBase
{
    public KMeansMenuFactory(IEventAggregator eventAggregator, INodeInfoProvider nodeInfoProvider) : base(eventAggregator, nodeInfoProvider)
    {
    }

    protected override INodeDisplayInfo DisplayInfo => KMeansAnalysis.DisplayInfo;
    protected override string NodeUniqueId => KMeansAnalysis.UniqueId;
    public override AnalysisMenuLocation Location { get; } = AnalysisMenuLocation.Analysis;

    public override string[]? AllowedTypes { get; } = new string[]
    {
        VoxelizationAnalysis.UniqueId,
    };
}
