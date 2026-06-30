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
        containerRegistry.Register<IAnalysisMenuFactory, PcaNodeMenuFactory>(nameof(PcaNodeMenuFactory));
        containerRegistry.Register<object, PcaViewModel>(PcaViewModel.UniqueId);

        containerRegistry.Register<object, VoxelizationAnalysis>(VoxelizationAnalysis.UniqueId);
        containerRegistry.RegisterInstance(VoxelizationAnalysis.DisplayInfo, VoxelizationAnalysis.UniqueId);
        containerRegistry.Register<IAnalysisMenuFactory, VoxelizationMenuFactory>(nameof(VoxelizationMenuFactory));

        containerRegistry.Register<object, KMeansAnalysis>(KMeansAnalysis.UniqueId);
        containerRegistry.RegisterInstance(KMeansAnalysis.DisplayInfo, KMeansAnalysis.UniqueId);
        containerRegistry.Register<IAnalysisMenuFactory, KMeansMenuFactory>(nameof(KMeansMenuFactory));
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var extensionRegistry = containerProvider.Resolve<IExtensionRegistry>();

        extensionRegistry.RegisterAnalysisView<PcaView, PcaViewModel>(AnalysisViewLocation.Default);

        extensionRegistry.RegisterOptions<PcaGlobalOptions>("Principal Component Analysis");
    }
}
