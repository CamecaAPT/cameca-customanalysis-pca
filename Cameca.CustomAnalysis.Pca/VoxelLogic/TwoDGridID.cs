using System;

public struct TwoDGridID : IComparable<TwoDGridID>
{

    public TwoDGridID(int firstDim, int secondDim) : GridID(GridID.GridLetterForIndex(firstDim) + GridID.GridLetterForIndex(secondDim))
    {
    }

    public (int, int) AsIndexPair()
    {
        int firstIndex = GridID.IndexForGridLetter((char)stringValue[0]);
        int secondIndex = GridID.IndexForGridLetter((char)stringValue[1]);
        return (firstIndex, secondIndex);
    }

    public int CompareTo(TwoDGridID other)
    {
        return stringValue.CompareTo(other.stringValue) ;
    }
}
 
 





