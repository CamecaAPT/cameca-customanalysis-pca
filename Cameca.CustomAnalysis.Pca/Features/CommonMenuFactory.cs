using Cameca.CustomAnalysis.Interface;
using Prism.Commands;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Cameca.CustomAnalysis.Pca;

internal delegate bool CheckEnabled(string nodeType);

internal class CommonMenuFactory : IAnalysisMenuFactory
{
    private const string SubMenuTitle = "PCA Suite";
    private const string? SubMenuToolTip = null;

    private readonly IEventAggregator eventAggregator;
    private readonly INodeInfoProvider nodeInfoProvider;

    public CommonMenuFactory(IEventAggregator eventAggregator, INodeInfoProvider nodeInfoProvider)
    {
        this.eventAggregator = eventAggregator;
        this.nodeInfoProvider = nodeInfoProvider;
    }

    public AnalysisMenuLocation Location { get; } = AnalysisMenuLocation.Analysis;

    public IMenuItem CreateMenuItem(IAnalysisMenuContext context)
    {
        var sourceNodeId = context.NodeId;
        if (nodeInfoProvider.Resolve(sourceNodeId) is not { TypeId: string nodeType })
        {
            return new SubMenu(SubMenuTitle, isEnabled: false, toolTip: SubMenuToolTip);
        }
        else
        {
            var menuItems = new List<IMenuItem>();
            if (IsVoxelizationEnabled(nodeType))
            {
                menuItems.Add(CreateChildMenuItem(VoxelizationAnalysis.UniqueId, VoxelizationAnalysis.DisplayInfo));
            }
            if (IsPcaEnabled(nodeType))
            {
                menuItems.Add(CreateChildMenuItem(PrincipalComponentAnalysis.UniqueId, PrincipalComponentAnalysis.DisplayInfo));
            }
            if (IsKMeansEnabled(nodeType))
            {
                menuItems.Add(CreateChildMenuItem(KMeansAnalysis.UniqueId, KMeansAnalysis.DisplayInfo));
            }
            if (IsSelectComponentIsovalueEnabled(nodeType))
            {
                menuItems.Add(CreateChildMenuItem(SelectComponentIsovalueAnalysis.UniqueId, SelectComponentIsovalueAnalysis.DisplayInfo));
            }
            if (IsMnMMEnabled(nodeType))
            {
                menuItems.Add(CreateChildMenuItem(MnMMAnalysis.UniqueId, MnMMAnalysis.DisplayInfo));
            }
            if (IsNegMnMMEnabled(nodeType))
            {
                menuItems.Add(CreateChildMenuItem(NegMnMMAnalysis.UniqueId, NegMnMMAnalysis.DisplayInfo));
            }
            if (IsGMMEnabled(nodeType))
            {
                menuItems.Add(CreateChildMenuItem(GaussianMixtureModelAnalysis.UniqueId, GaussianMixtureModelAnalysis.DisplayInfo));
            }
            if (IsONMFEnabled(nodeType))
            {
                menuItems.Add(CreateChildMenuItem(OrthNonNegMatrixFactorizationAnalysis.UniqueId, OrthNonNegMatrixFactorizationAnalysis.DisplayInfo));
            }
            
            return new SubMenu(SubMenuTitle, isEnabled: true, toolTip: SubMenuToolTip)
            {
                MenuItems = menuItems,
            };
        }

        IMenuItem CreateChildMenuItem(string targetType, INodeDisplayInfo targetDisplayInfo, CheckEnabled? resolveEnabled = null, string? toolTip = null)
        {
            return new MenuAction(targetDisplayInfo.Title, new DelegateCommand(() =>
            {
                eventAggregator.PublishCreateNode(targetType, sourceNodeId, targetDisplayInfo.Title, targetDisplayInfo.Icon);
            }), targetDisplayInfo.Icon, resolveEnabled?.Invoke(nodeType) ?? true, toolTip);
        }
    }

    // Voxelization is the root - enabled only when not a child of a PCA Suite analysis node
    private bool IsVoxelizationEnabled(string nodeType) => !nodeType.StartsWith("Cameca.CustomAnalysis.Pca.");
    private CheckEnabled IsPcaEnabled = IsAnyOf(VoxelizationAnalysis.UniqueId);
    private CheckEnabled IsKMeansEnabled = IsAnyOf(VoxelizationAnalysis.UniqueId, PrincipalComponentAnalysis.UniqueId);
    private CheckEnabled IsMnMMEnabled = IsAnyOf(VoxelizationAnalysis.UniqueId);
    private CheckEnabled IsNegMnMMEnabled = IsAnyOf(VoxelizationAnalysis.UniqueId);
    private CheckEnabled IsSelectComponentIsovalueEnabled = IsAnyOf(PrincipalComponentAnalysis.UniqueId);
    private CheckEnabled IsGMMEnabled = IsAnyOf(VoxelizationAnalysis.UniqueId);
    private CheckEnabled IsONMFEnabled = IsAnyOf(VoxelizationAnalysis.UniqueId);

    private static CheckEnabled IsAnyOf(params string[] allowedNodeTypes) => (string nodeType) => allowedNodeTypes.Contains(nodeType);
}
