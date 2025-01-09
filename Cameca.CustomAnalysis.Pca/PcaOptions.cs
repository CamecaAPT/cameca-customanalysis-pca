using Prism.Mvvm;
using System.ComponentModel.DataAnnotations;

namespace Cameca.CustomAnalysis.Pca;

public class PcaOptions : BindableBase
{
    private int components = 0;
    public int Components
    {
        get => components;
        set => SetProperty(ref components, value);
    }

    private int componentIndex = 0;
    [Display(Name = "Component Index")]
    public int ComponentIndex
    {
        get => componentIndex;
        set => SetProperty(ref componentIndex, value);
    }

    private float isovalue = 1f;
    public float Isovalue
    {
        get => isovalue;
        set => SetProperty(ref isovalue, value);
    }
}
