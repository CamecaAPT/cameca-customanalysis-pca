using System.Collections.Generic;
using System.Diagnostics;
namespace Cameca.CustomAnalysis.Pca.VoxelLogic;;

public struct OneDGridID : IStringConvertible
{
    string stringValue;
    public OneDGridID(string s)
    {
        this.stringValue = s;
    }
    public OneDGridID(int firstDim)
    {
        string firstLetter = GridID.GridLetterForIndex(firstDim);
        this.stringValue = firstLetter;
    }
    public int PCAIndex()
    {
        return GridID.IndexForGridLetter(stringValue[0]);
    }
    public override string ToString()
    {
        return stringValue;
    }
    public int CompareTo(IStringConvertible other)
    {
        return stringValue.CompareTo(other.ToString());
    }
}