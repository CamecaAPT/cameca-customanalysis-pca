using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using Cameca.CustomAnalysis.Utilities.Segmentation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Cameca.CustomAnalysis.Pca;

internal abstract partial class BaseClusteringAnalysis<TProperties> : StandardAnalysisFilterNodeBase<TProperties>
    where TProperties : BaseClusteringProperties, new()
{

    protected readonly SegmentedRoiManager<IStandardAnalysisFilterNodeBaseServices> segmentedManager;

    protected BaseClusteringAnalysis(
        IStandardAnalysisFilterNodeBaseServices services,
        ResourceFactory resourceFactory)
        : base(services, resourceFactory)
    {
        segmentedManager = SegmentedRoiManager.Create(this);
    }

    protected override void OnAdded(NodeAddedEventArgs eventArgs)
    {
        base.OnAdded(eventArgs);
        if (eventArgs.Trigger == EventTrigger.Create)
        {
            var ownderNode = eventArgs.NodeId;
            SyncPlaceholderExpectedSegments(Properties.ClusterCount);
        }
    }

    protected override void OnPropertiesChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertiesChanged(e);
        CanSave = true;
        DataStateIsValid = false;
        if (e.PropertyName == nameof(Properties.ClusterCount))
        {
            SyncPlaceholderExpectedSegments(Properties.ClusterCount);
        }
    }

    // Returns an int array of assignments to each sparse voxel (should match same length as sparse voxel index map)
    protected abstract int[] Cluster(IIonData ionData, VoxelFeatureMatrixSectionData voxelFeatureMatrixSectionData);

    protected void RunCluster(IIonData ionData)
    {
        if (DataStateIsValid && !DataStateIsError)
        {
            return;
        }
        int clusters = Properties.ClusterCount;
        var data = ionData.GetVoxelFeatureMatrixSectionData(Resources.Parent!.DataSectionName);

        var clusterResults = Cluster(ionData, data);

        // If for whatever reason, there are more assignments than clusteres, eliminated extras (floating point errors?)
        // and represent them as ignored byte.MaxValue
        var sanitizedResults = new byte[clusterResults.Length];
        for (int i = 0; i < clusterResults.Length; ++i)
        {
            int raw = clusterResults[i];
            byte sanitized = raw < clusters ? (byte)raw : byte.MaxValue;
            sanitizedResults[i] = sanitized;
        }

        IonAssignmentSerializer.WriteToDataSection(
            ionData,
            Resources.DataSectionName,
            data.ExtraData.GridParameters,
            data.VoxelFeatureMatrix.GetVoxelIndices(),
            sanitizedResults);

        UpdateSegmentedChildrenRois();
        DataStateIsValid = true;
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    protected override async IAsyncEnumerable<ReadOnlyMemory<ulong>> GetIndicesDelegateAsync(
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        IIonData ownerIonData,
        IProgress<double>? progress,
        [EnumeratorCancellation] CancellationToken token)
    {
        RunCluster(ownerIonData);
        foreach (var chunk in ownerIonData.AllowAllFilter(progress, token))
        {
            yield return chunk;
        }
    }

    protected void UpdateSegmentedChildrenRois()
    {
        segmentedManager.UpdateSync(
            getSegmentTitle: GetSegmentTitle,
            excludeIds: new[] { byte.MaxValue },
            cancellationToken: default);
    }

    protected virtual string GetSegmentTitle(byte id)
    {
        foreach (var child in Resources.Children)
        {
            if (Services.InstanceProvider.Resolve(child.Id) is SegmentedRoiNode roiNode
                && roiNode.Properties.FilterValue == id
                && Services.NodeInfoProvider.Resolve(child.Id) is { } nodeInfo)
            {
                return nodeInfo.Name;
            }
        }

        return $"Cluster {id + 1}";
    }

    private void SyncPlaceholderExpectedSegments(int segmentCount)
    {
        var segmentationValues = Enumerable.Range(0, segmentCount).Select(i => (byte)i).ToHashSet();

        foreach (var child in Resources.Children)
        {
            if (Services.InstanceProvider.Resolve(child.Id) is not SegmentedRoiNode roiNode)
            {
                continue;
            }

            // pop from the copy so we don't consider again
            if (segmentationValues.Contains(roiNode.Properties.FilterValue))
            {
                segmentationValues.Remove(roiNode.Properties.FilterValue);
            }
            // the child has a filter ID that is not in the expected list: just ignore it
        }
        // for everything remaining in the copy at this point, it didn't match so it needs to be created
        foreach (var newFilterId in segmentationValues)
        {
            var title = GetSegmentTitle(newFilterId);
            var newId = Resources.CreateChildNode(SegmentedRoiNode.UniqueId, Id, title);
            if (Services.InstanceProvider.Resolve(newId) is SegmentedRoiNode childFilterNode)
            {
                childFilterNode.Properties.FilterValue = newFilterId;
            }
        }
    }
}
