using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;

namespace Cameca.CustomAnalysis.Pca;

[XmlRoot("PcaOptions")]
public partial class PcaProperties : ObservableObject
{
    [ObservableProperty]
    private int components = 0;

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
}
