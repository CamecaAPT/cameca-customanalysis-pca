using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cameca.CustomAnalysis.Pca;

public abstract partial class BaseClusteringProperties : ObservableValidator
{
    [ObservableProperty]
    [field: Display(Name = "Clusters", Description = "Number of clusters to estimate. (Default 2)")]
    [field: Range(2, int.MaxValue)]
    private int clusterCount = 2;
}
