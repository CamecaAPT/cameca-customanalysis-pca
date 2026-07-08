using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cameca.CustomAnalysis.Pca;

public partial class GaussianMixtureModelProperties : ObservableValidator
{
    [ObservableProperty]
    [field: Display(Name = "Clusters", Description = "Number of clusters to estimate. (Default 2)")]
    [field: Range(2, int.MaxValue)]
    private int clusterCount = 2;

    [ObservableProperty]
    [field: Display(Name = "Replicates", Description = "Number of times to repeat analysis, returning the \"best\". (Default 10)")]
    [field: Range(1, int.MaxValue)]
    private int replicates = 10;

    [ObservableProperty]
    [field: Display(Name = "Regularization", Description = "Regularization parameter, isotropic variance added Sigma")]
    private float regParam = 0f;
}
