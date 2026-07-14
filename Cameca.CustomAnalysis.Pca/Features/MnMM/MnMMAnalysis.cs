using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Pca;
using Cameca.CustomAnalysis.PcaLib.Interface;
using Cameca.CustomAnalysis.Utilities;

namespace Cameca.CustomAnalysis;

[DefaultView(MnMMViewModel.UniqueId, typeof(MnMMViewModel))]
internal partial class MnMMAnalysis : BaseClusteringAnalysis<MnMMProperties>
{
    public const string UniqueId = "Cameca.CustomAnalysis.Pca.MnMMAnalysis";

    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("Multinomial Mixture Model");

    public MnMMAnalysis(
        IStandardAnalysisFilterNodeBaseServices services,
        ResourceFactory resourceFactory)
        : base(services, resourceFactory)
    {
    }

    protected override int[] Cluster(IIonData ionData, VoxelFeatureMatrixSectionData voxelFeatureMatrixSectionData)
    {
        var clusterer = new MultinomialMixtureModel(voxelFeatureMatrixSectionData.VoxelFeatureMatrix);
        var clusterResults = clusterer.TrainModel(Properties.ClusterCount, Properties.Replicates);
        return clusterResults.VoxelIndex;
    }
}
