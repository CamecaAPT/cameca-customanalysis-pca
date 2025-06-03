using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;

namespace Cameca.CustomAnalysis.Pca;

public enum BornemannTableSignificance
{
    [Display(Name = "0.999")]
    Sig999  = 0,
    [Display(Name = "0.995")]
    Sig995 = 1,
    [Display(Name = "0.990")]
    Sig990 = 2,
    [Display(Name = "0.980")]
    Sig980 = 3,
    [Display(Name = "0.950")]
    Sig950 = 4,
    [Display(Name = "0.900")]
    Sig900 = 5,
}

public enum GridMethod
{
    [Display(Name = "Ion Types")]
    IonTypes = 0,
    [Display(Name = "Peaks")]
    Peaks = 1,
}

[XmlRoot("PcaOptions")]
public partial class PcaProperties : ObservableObject
{
    [ObservableProperty]
    [field: Display(Name = "Grid Method")]
    private GridMethod gridMethod = GridMethod.IonTypes;

    [ObservableProperty]
    [field: Display(Name = "Voxel Size (nm)")]
    private float voxelSize = 1f;

    [ObservableProperty]
    [field: Display(Name = "Voxel Grid Edge Buffer")]
    private float voxelGridEdgeBuffer = 1.5f;

    [ObservableProperty]
    private int gaps = 1;

    [ObservableProperty]
    private BornemannTableSignificance significance = BornemannTableSignificance.Sig995;

    [ObservableProperty]
    private bool refine = true;

    [ObservableProperty]
    [field: Display(Name = "Components")]
    private int numberOfComponents = 0;

    [ObservableProperty]
    [field:Display(Name = "Component Index")]
    private int componentIndex = 0;

    [ObservableProperty]
    private float isovalue = 1f;

    [ObservableProperty]
    [field:ReadOnly(true)]
    private float? min = null;

    [ObservableProperty]
    [field:ReadOnly(true)]
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

    [ObservableProperty]
    [field: Display(Name = "Use PCA Phase for Detatched ROI")]
    private bool usePCAPhaseForDetatchedROI = true;

    [Display(AutoGenerateField = false)]
    public SerializableColorMap? PcaColorMap { get; set; }

    [Display(AutoGenerateField = false)]
    public SerializableColorMap? ComponentsColorMap { get; set; }

    [Display(AutoGenerateField = false)]
    [ObservableProperty]
    private bool logScaleY = true;
}

public class SerializableColorMap
{
    public Color Bottom { get; set; }
    public Color NanColor { get; set; }
    public Color OutOfRangeBottom { get; set; }
    public Color OutOfRangeTop { get; set; }
    public Color Top { get; set; }
    public List<SerializableColorStop> ColorStops { get; set; } = new();
    public float BottomValue { get; set; }
    public float TopValue { get; set; }
}

public class SerializableColorStop
{
    public Color BottomColor { get; set; }
    public float RelativePosition { get; set; }
    public Color TopColor { get; set; }
}