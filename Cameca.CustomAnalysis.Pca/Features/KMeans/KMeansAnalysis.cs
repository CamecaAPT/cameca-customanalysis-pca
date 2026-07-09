using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.PcaLib.Interface;
using Cameca.CustomAnalysis.Utilities;
using System.Collections;
using System.Collections.Generic;

namespace Cameca.CustomAnalysis.Pca;

internal partial class KMeansAnalysis : BaseClusteringAnalysis<KMeansProperties>
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.KMeansAnalysis";

    public KMeansAnalysis(
        IStandardAnalysisFilterNodeBaseServices services,
        ResourceFactory resourceFactory)
        : base(services, resourceFactory)
    {
    }

    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("K-Means Clustering");
    
    protected override int[] Cluster(IIonData ionData, VoxelFeatureMatrixSectionData data)
    {
        var clusterer = data.LoadingsMatrix is not null
                ? new ClustererKMeans(data.VoxelFeatureMatrix, ToNested(data.LoadingsMatrix))
                : new ClustererKMeans(data.VoxelFeatureMatrix);
        var clusterResults = clusterer.Cluster(Properties.ClusterCount, Properties.Replicates, Properties.Weighted);
        return clusterResults.VoxelIndex;
    }

    private static IEnumerable<IEnumerable<float>> ToNested(VoxelFeatureMatrix matrix)
    {
        for (var i = 0; i < matrix.FeatureCount; i++)
        {
            yield return matrix.GetFeatureData(i).ToArray();
        }
    }
}
