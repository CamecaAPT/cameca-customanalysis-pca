using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.PcaLib.Interface;
using Cameca.CustomAnalysis.Utilities;
using Cameca.CustomAnalysis.Utilities.Segmentation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Cameca.CustomAnalysis.Pca;

internal partial class KMeansAnalysis : StandardAnalysisFilterNodeBase<KMeansProperties>
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.KMeansAnalysis";

    private readonly SegmentedRoiManager<IStandardAnalysisFilterNodeBaseServices> segmentedManager;

    public KMeansAnalysis(
        IStandardAnalysisFilterNodeBaseServices services,
        ResourceFactory resourceFactory)
        : base(services, resourceFactory)
    {
        segmentedManager = SegmentedRoiManager.Create(this);
    }

    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("K-Means Clustering");

    protected override void OnPropertiesChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertiesChanged(e);
        CanSave = true;
        DataStateIsValid = false;
    }

    protected override void OnAdded(NodeAddedEventArgs eventArgs)
    {
        base.OnAdded(eventArgs);
        if (eventArgs.Trigger == EventTrigger.Create)
        {
            var ownderNode = eventArgs.NodeId;
            foreach (var newFilterId in new HashSet<byte>(Enumerable.Range(0, Properties.ClusterCount).Select(x => (byte)x)))
            {
                var title = GetSegmentTitle(newFilterId);
                var newId = Resources.CreateChildNode(SegmentedRoiNode.UniqueId, ownderNode, title);
                if (Services.InstanceProvider.Resolve(newId) is SegmentedRoiNode childFilterNode)
                {
                    childFilterNode.Properties.FilterValue = newFilterId;
                }
            }
        }
    }

    private VoxelFeatureMatrixSectionData GetVoxelFeatureData(IIonData ionData)
    {
        // This isn't the top level -- .Parent! is safe
        if (VoxelFeatureMatrixSerializer.ReadFromIonDataSection(ionData, Resources.Parent!.DataSectionName) is not { } data)
        {
            throw new InvalidOperationException("Parent must define a VoxelFeatureMatrix");
        }
        return data;
    }

    private void Cluster(IIonData ionData)
    {
        if (DataStateIsValid && !DataStateIsError)
        {
            return;
        }
        int clusters = Properties.ClusterCount;
        var data = GetVoxelFeatureData(ionData);

        var clusterer = new ClustererKMeans(data.VoxelFeatureMatrix);
        var clusterResults = clusterer.Cluster(clusters, Properties.Replicates, Properties.Weighted);

        // If for whatever reason, there are more assignments than clusteres, eliminated extras (floating point errors?)
        // and represent them as ignored byte.MaxValue
        var sanitizedResults = new byte[clusterResults.VoxelIndex.Length];
        for (int i = 0; i < clusterResults.VoxelIndex.Length; ++i)
        {
            int raw = clusterResults.VoxelIndex[i];
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
        Cluster(ownerIonData);
        foreach (var chunk in ownerIonData.AllowAllFilter(progress, token))
        {
            yield return chunk;
        }
    }

    private void UpdateSegmentedChildrenRois()
    {
        segmentedManager.UpdateSync(
            getSegmentTitle: GetSegmentTitle,
            excludeIds: new[] { byte.MaxValue },
            cancellationToken: default);
    }

    private static string GetSegmentTitle(byte id) => $"Cluster {id}";
}
