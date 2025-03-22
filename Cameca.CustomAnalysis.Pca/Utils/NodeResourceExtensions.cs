using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using System;
using System.Linq;

namespace Cameca.CustomAnalysis.Pca;

internal static class NodeResourceExtensions
{
    public static INodeResource FindGridNode(this INodeResource nodeResource, INodeDataProvider nodeDataProvider)
    {
        var parentRoi = FindParentCubeRoi(nodeResource, nodeDataProvider);
        var grid3dNode = FindImmediateChildGrid3dNode(parentRoi);
        return grid3dNode;
    }

    private static INodeResource FindParentCubeRoi(INodeResource nodeResource, INodeDataProvider nodeDataProvider)
    {
        INodeResource? parent = nodeResource.Parent;
        while (parent is not null)
        {
            if (nodeDataProvider.Resolve(parent.Id) is { } dataNode)
            {
                if (parent.Region?.Shape == Shape.Cube)
                {
                    return parent;
                }
                else
                {
                    throw new InvalidOperationException("Analysis must only be added to a Cube ROI");
                }
            }

            // Else set next parent
            parent = parent.Parent;
        }
        throw new InvalidOperationException("Could not resolve a parent ROI for the analysis");
    }

    private static INodeResource FindImmediateChildGrid3dNode(INodeResource nodeResource)
    {
        return nodeResource.Children.Single(node => node.TypeId == "GridNode");
    }
}
