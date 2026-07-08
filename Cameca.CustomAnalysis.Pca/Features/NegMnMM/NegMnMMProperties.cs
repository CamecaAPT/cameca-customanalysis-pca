using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel.DataAnnotations;

namespace Cameca.CustomAnalysis.Pca;

public partial class NegMnMMProperties : BaseClusteringProperties
{
    [ObservableProperty]
    [field: Display(Name = "Replicates", Description = "Number of times to repeat analysis, returning the \"best\". (Default 10)")]
    [field: Range(1, int.MaxValue)]
    private int replicates = 10;
}