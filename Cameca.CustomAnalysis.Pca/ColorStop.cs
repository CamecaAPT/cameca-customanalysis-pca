using Cameca.CustomAnalysis.Interface;
using Prism.Mvvm;
using System.Windows.Media;

namespace Cameca.CustomAnalysis.Pca;

internal class ColorStop : BindableBase, IColorStop
{
    private Color topColor;
    public Color TopColor
    {
        get => topColor;
        set => SetProperty(ref topColor, value);
    }

    private float relativePosition;
    public float RelativePosition
    {
        get => relativePosition;
        set => SetProperty(ref relativePosition, value);
    }

    private Color bottomColor;
    public Color BottomColor
    {
        get => bottomColor;
        set => SetProperty(ref bottomColor, value);
    }
}