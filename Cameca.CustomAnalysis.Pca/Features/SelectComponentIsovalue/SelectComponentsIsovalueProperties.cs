using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Cameca.CustomAnalysis.Pca;

public partial class SelectComponentIsovalueProperties : ObservableValidator
{
    [ObservableProperty]
    [field: Display(Name = "Component")]
    [field: Range(1, int.MaxValue)]
    private int componentNumber = 1;

    [ObservableProperty]
    private float isovalue = 1f;
}

