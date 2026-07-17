using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Cameca.CustomAnalysis.Pca;

public partial class SpatialPartitioningProperties : ObservableObject
{
    [ObservableProperty]
    [field: Display(Name = "Component Index")]
    private int componentIndex = 0;

    [ObservableProperty]
    [field: ReadOnly(true)]
    private float? min = null;

    [ObservableProperty]
    [field: ReadOnly(true)]
    private float? max = null;

    [ObservableProperty]
    private bool invert = false;

    [ObservableProperty]
    [field: Display(Name = "Grid Projection Bin Size")]
    private float gridProjectionBinSize = 0.5f;

    [ObservableProperty]
    [field: Display(Name = "Grid Projection Delocalization")]
    private float gridProjectionDelocalization = 0.25f;

    [ObservableProperty]
    [field: Display(Name = "Peak ID Noise Floor")]
    private float noiseFloorFraction = 0.05f;

    [ObservableProperty]
    [field: Display(Name = "Peak Summit Allowance")]
    private float peakSummitAllowance = 0.8f;

    [ObservableProperty]
    [field: Display(Name = "PCA Phase Index")]
    private int pcaPhaseIndex = 0;

    [Display(AutoGenerateField = false)]
    public SerializableColorMap? PcaColorMap { get; set; }

    [Display(AutoGenerateField = false)]
    public List<string> GridsToUseForPCA { get; set; } = new();

    [Display(AutoGenerateField = false)]
    public List<string> HistogramsToUseForPCA { get; set; } = new();
}
