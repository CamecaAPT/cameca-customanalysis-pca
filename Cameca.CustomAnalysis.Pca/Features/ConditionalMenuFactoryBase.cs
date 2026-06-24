using Cameca.CustomAnalysis.Interface;
using Prism.Commands;
using Prism.Events;
using System.Linq;

namespace Cameca.CustomAnalysis.Pca;

internal abstract class ConditionalMenuFactoryBase : IAnalysisMenuFactory
{
    private readonly IEventAggregator eventAggregator;
    private readonly INodeInfoProvider nodeInfoProvider;

    protected ConditionalMenuFactoryBase(IEventAggregator eventAggregator, INodeInfoProvider nodeInfoProvider)
    {
        this.eventAggregator = eventAggregator;
        this.nodeInfoProvider = nodeInfoProvider;
    }


    public abstract AnalysisMenuLocation Location { get; }
    protected abstract INodeDisplayInfo DisplayInfo { get; }
    protected abstract string NodeUniqueId { get; }
    public virtual string? ToolTip { get; }

    public virtual string[]? AllowedTypes { get; } = null;
    public virtual string[]? DisallowedTypes { get; } = null;

    public IMenuItem CreateMenuItem(IAnalysisMenuContext context)
    {
        var nodeInfo = nodeInfoProvider.Resolve(context.NodeId);
        if (nodeInfoProvider.Resolve(context.NodeId) is { TypeId: string nodeType } && ShowMenuItem(nodeType))
        {
            IAnalysisMenuContext context2 = context;
            return new MenuAction(DisplayInfo.Title, new DelegateCommand(() =>
            {
                eventAggregator.PublishCreateNode(NodeUniqueId, context2.NodeId, DisplayInfo.Title, DisplayInfo.Icon);
            }), DisplayInfo.Icon, true, ToolTip);
        }
        return null;
    }

    private bool ShowMenuItem(string nodeType)
    {
        return IsAllowed(nodeType) && !IsDisallowed(nodeType);

        bool IsAllowed(string nodeId) => AllowedTypes is null || AllowedTypes.Contains(nodeId);
        bool IsDisallowed(string nodeId) => DisallowedTypes is not null && DisallowedTypes.Contains(nodeId);
    }
}
