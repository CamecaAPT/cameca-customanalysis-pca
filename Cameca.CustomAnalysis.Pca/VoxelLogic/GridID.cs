using System;

public struct GridID : IComparable<GridID>
{
    string stringValue;

    public GridID(string value)
    {
        this.stringValue = value;
    }

    public static string GridLetterForIndex(int index)
    {
        int AAsciiValue = (int)'A';
        char cha = (char)(AAsciiValue + index);
        return cha.ToString();
    }
    public static int IndexForGridLetter(char letter)
    {
        int AAsciiValue = (int)'A'; 
        int gridLetterAsciiValue = (int)letter;
        return gridLetterAsciiValue - AAsciiValue;
    }
    public string ToString()
    {
        return stringValue;
    }

    public int CompareTo(GridID other)
    {
        return stringValue.CompareTo(other.stringValue) ;
    }
}
 
 





