using System;
namespace Cameca.CustomAnalysis.Pca.VoxelLogic;
public struct TwoDGridID : IStringConvertible
{
    string stringValue;

    public TwoDGridID(int firstDim, int secondDim)  
    {
        stringValue = GridID.GridLetterForIndex(firstDim) + GridID.GridLetterForIndex(secondDim);
    }

    public (int, int) AsIndexPair()
    {
        int firstIndex = GridID.IndexForGridLetter((char)stringValue[0]);
        int secondIndex = GridID.IndexForGridLetter((char)stringValue[1]);
        return (firstIndex, secondIndex);
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
 
 





