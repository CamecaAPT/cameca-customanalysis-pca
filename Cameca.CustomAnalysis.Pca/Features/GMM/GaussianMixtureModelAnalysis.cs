using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.PcaLib.Interface;
using Cameca.CustomAnalysis.Utilities;

namespace Cameca.CustomAnalysis.Pca;

internal partial class GaussianMixtureModelAnalysis : BaseClusteringAnalysis<GaussianMixtureModelProperties>
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.GaussianMixtureModelAnalysis";

    public GaussianMixtureModelAnalysis(
        IStandardAnalysisFilterNodeBaseServices services,
        ResourceFactory resourceFactory)
        : base(services, resourceFactory)
    {
    }

    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("Gaussian Mixture Model");

    protected override int[] Cluster(IIonData ionData, VoxelFeatureMatrixSectionData voxelFeatureMatrixSectionData)
    {
        var clusterer = new GaussianMixtureModel(voxelFeatureMatrixSectionData.VoxelFeatureMatrix);
        var clusterResults = clusterer.TrainModel(Properties.ClusterCount, Properties.Replicates, Properties.RegParam);
        return clusterResults.VoxelIndex;
    }
}
