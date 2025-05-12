using System;

public struct OneDGridID : GridID, IComparable<OneDGridID>
{
    public OneDGridID(int firstDim) : GridID(GridID.GridLetterForIndex(firstDim))
    {
    }

    public int AsIndex()
    {
        return GridID.IndexForGridLetter((char)stringValue[0]);
    }

    public int CompareTo(TwoDGridID other)
    {
        return stringValue.CompareTo(other.stringValue) ;
    }
}
 
 





