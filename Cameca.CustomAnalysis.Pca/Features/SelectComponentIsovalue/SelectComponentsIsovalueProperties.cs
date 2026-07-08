using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Cameca.CustomAnalysis.Pca;

public enum IsovalueComparison
{
    GreaterOrEqual = 0,
    GreaterThan = 1,
    LessOrEqual = 2,
    LessThan = 3,
}

public partial class SelectComponentIsovalueProperties : ObservableValidator
{
    [ObservableProperty]
    [field: Display(Name = "Component")]
    [field: Range(1, int.MaxValue)]
    private int componentNumber = 1;

    [ObservableProperty]
    private float isovalue = 1f;

    [ObservableProperty]
    [field: Display(Name = "Comparison")]
    private IsovalueComparison comparison = IsovalueComparison.GreaterOrEqual;
}

