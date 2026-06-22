using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Cameca.CustomAnalysis.Pca;

public partial class VoxelizationProperties : ObservableObject
{
    [ObservableProperty]
    [field: Display(Name = "Grid Method")]
    private GridMethod gridMethod = GridMethod.IonTypes;

    [ObservableProperty]
    [field: Display(Name = "Bin Size (for Bins grid method)")]
    private double binSize = 1d;

    [ObservableProperty]
    [field: Display(Name = "Bin Start Value (for Bins grid method)")]
    private float? binStart = null;

    [ObservableProperty]
    [field: Display(Name = "Bin End Value (for Bins grid method)")]
    private float? binEnd = null;

    [ObservableProperty]
    [field: Display(Name = "Voxel Size (nm)")]
    private float voxelSize = 1f;

    [ObservableProperty]
    [field: Display(Name = "Voxel Grid Edge Buffer")]
    private float voxelGridEdgeBuffer = 1.5f;
}
