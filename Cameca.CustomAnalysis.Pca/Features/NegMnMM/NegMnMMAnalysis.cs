using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.PcaLib.Interface;
using Cameca.CustomAnalysis.Utilities;

namespace Cameca.CustomAnalysis.Pca;

internal partial class NegMnMMAnalysis : BaseClusteringAnalysis<NegMnMMProperties>
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.NegMnMMAnalysis";

    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("Negative Multinomial Mixture Model");

    public NegMnMMAnalysis(
        IStandardAnalysisFilterNodeBaseServices services,
        ResourceFactory resourceFactory)
        : base(services, resourceFactory)
    {
    }

    protected override int[] Cluster(IIonData ionData, VoxelFeatureMatrixSectionData voxelFeatureMatrixSectionData)
    {
        var clusterer = new NegMultinomialMixtureModel(voxelFeatureMatrixSectionData.VoxelFeatureMatrix);
        var clusterResults = clusterer.TrainModel(Properties.ClusterCount, Properties.Replicates);
        return clusterResults.VoxelIndex;
    }
}
