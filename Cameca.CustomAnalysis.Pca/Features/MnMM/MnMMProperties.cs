using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel.DataAnnotations;

namespace Cameca.CustomAnalysis.Pca;

public partial class MnMMProperties : ObservableValidator
{
    [ObservableProperty]
    [field: Display(Name = "Clusters", Description = "Number of clusters to estimate. (Default 2)")]
    [field: Range(2, int.MaxValue)]
    private int clusterCount = 2;

    [ObservableProperty]
    [field: Display(Name = "Replicates", Description = "Number of times to repeat analysis, returning the \"best\". (Default 10)")]
    [field: Range(1, int.MaxValue)]
    private int replicates = 10;
}