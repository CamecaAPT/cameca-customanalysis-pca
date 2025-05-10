using System;

public struct TwoDGridID : IComparable<TwoDGridID>
{
    string stringValue;


    public TwoDGridID(int firstDim, int secondDim)
    {
        string firstLetter = GridLetterForIndex(firstDim);
        string secondLetter = GridLetterForIndex(secondDim);
        this.stringValue = firstLetter + secondLetter;
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

    public (int, int) AsIndexPair()
    {
        int firstIndex = IndexForGridLetter((char)stringValue[0]);
        int secondIndex = IndexForGridLetter((char)stringValue[1]);
        return (firstIndex, secondIndex);
    }

    public int CompareTo(TwoDGridID other)
    {
        return stringValue.CompareTo(other.stringValue) ;
    }
}
 
 





