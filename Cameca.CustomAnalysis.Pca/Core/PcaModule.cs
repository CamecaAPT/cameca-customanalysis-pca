using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using Prism.Ioc;
using Prism.Modularity;

namespace Cameca.CustomAnalysis.Pca;

/// <summary>
/// Public <see cref="IModule"/> implementation is the entry point for AP Suite to discover and configure the custom analysis
/// </summary>
public class PcaModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.AddCustomAnalysisUtilities(options => options.UseStandardBaseClasses = true);

        containerRegistry.RegisterSegmentedRoi();

        containerRegistry.Register<object, PrincipalComponentAnalysis>(PrincipalComponentAnalysis.UniqueId);
        containerRegistry.RegisterInstance(PrincipalComponentAnalysis.DisplayInfo, PrincipalComponentAnalysis.UniqueId);
        containerRegistry.Register<object, PcaViewModel>(PcaViewModel.UniqueId);

        containerRegistry.Register<object, VoxelizationAnalysis>(VoxelizationAnalysis.UniqueId);
        containerRegistry.RegisterInstance(VoxelizationAnalysis.DisplayInfo, VoxelizationAnalysis.UniqueId);

        containerRegistry.Register<object, KMeansAnalysis>(KMeansAnalysis.UniqueId);
        containerRegistry.RegisterInstance(KMeansAnalysis.DisplayInfo, KMeansAnalysis.UniqueId);

        containerRegistry.Register<object, MnMMAnalysis>(MnMMAnalysis.UniqueId);
        containerRegistry.RegisterInstance(MnMMAnalysis.DisplayInfo, MnMMAnalysis.UniqueId);

        containerRegistry.Register<object, NegMnMMAnalysis>(NegMnMMAnalysis.UniqueId);
        containerRegistry.RegisterInstance(NegMnMMAnalysis.DisplayInfo, NegMnMMAnalysis.UniqueId);

        containerRegistry.Register<object, GaussianMixtureModelAnalysis>(GaussianMixtureModelAnalysis.UniqueId);
        containerRegistry.RegisterInstance(GaussianMixtureModelAnalysis.DisplayInfo, GaussianMixtureModelAnalysis.UniqueId);

        containerRegistry.Register<object, SelectComponentIsovalueAnalysis>(SelectComponentIsovalueAnalysis.UniqueId);
        containerRegistry.RegisterInstance(SelectComponentIsovalueAnalysis.DisplayInfo, SelectComponentIsovalueAnalysis.UniqueId);

        containerRegistry.Register<object, OrthNonNegMatrixFactorizationAnalysis>(OrthNonNegMatrixFactorizationAnalysis.UniqueId);
        containerRegistry.RegisterInstance(OrthNonNegMatrixFactorizationAnalysis.DisplayInfo, OrthNonNegMatrixFactorizationAnalysis.UniqueId);

        containerRegistry.Register<IAnalysisMenuFactory, CommonMenuFactory>(nameof(CommonMenuFactory));
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var extensionRegistry = containerProvider.Resolve<IExtensionRegistry>();

        extensionRegistry.RegisterAnalysisView<PcaView, PcaViewModel>(AnalysisViewLocation.Default);

        extensionRegistry.RegisterOptions<PcaGlobalOptions>("Principal Component Analysis");
    }
}
