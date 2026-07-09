using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.PcaLib.Interface;
using Cameca.CustomAnalysis.Utilities;

namespace Cameca.CustomAnalysis.Pca;

internal partial class OrthNonNegMatrixFactorizationAnalysis : BaseClusteringAnalysis<OrthNonNegMatrixFactorizationProperties>
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.OrthNonNegMatrixFactorizationAnalysis";

    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("Orthogonal Non-negative Matrix Factorization Model");

    public OrthNonNegMatrixFactorizationAnalysis(
        IStandardAnalysisFilterNodeBaseServices services,
        ResourceFactory resourceFactory)
        : base(services, resourceFactory)
    {
    }

    protected override int[] Cluster(IIonData ionData, VoxelFeatureMatrixSectionData voxelFeatureMatrixSectionData)
    {
        var clusterer = new OrthNonNegMatrixFactorization(voxelFeatureMatrixSectionData.VoxelFeatureMatrix);
        var clusterResults = clusterer.TrainModel(Properties.ClusterCount, Properties.Replicates, Properties.Weighted);
        return clusterResults.VoxelIndex;
    }
}
