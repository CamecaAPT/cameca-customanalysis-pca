using System.Collections.Generic;
using System.Diagnostics;
namespace Cameca.CustomAnalysis.Pca.VoxelLogic;

public readonly struct OneDGridID : IStringConvertible
{
    readonly string stringValue;
    public OneDGridID(string s)
    {
        this.stringValue = s;
    }
    public OneDGridID(int firstDim)
    {
        string firstLetter = GridID.GridLetterForIndex(firstDim);
        this.stringValue = firstLetter;
    }
    public readonly int PCAIndex()
    {
        return GridID.IndexForGridLetter(stringValue[0]);
    }
    public override readonly string ToString()
    {
        return stringValue;
    }
    public readonly int CompareTo(IStringConvertible other)
    {
        return stringValue.CompareTo(other.ToString());
    }
}