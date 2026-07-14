using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.PcaLib.Interface;
using Cameca.CustomAnalysis.Utilities;

namespace Cameca.CustomAnalysis.Pca;

[DefaultView(KMeansViewModel.UniqueId, typeof(KMeansViewModel))]
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
        var clusterer = new ClustererKMeans(data.VoxelFeatureMatrix);
        var clusterResults = clusterer.Cluster(Properties.ClusterCount, Properties.Replicates, Properties.Weighted);
        return clusterResults.VoxelIndex;
    }
}
