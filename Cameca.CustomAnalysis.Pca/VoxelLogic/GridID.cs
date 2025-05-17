using System;

namespace Cameca.CustomAnalysis.Pca.VoxelLogic;

public interface IStringConvertible : IComparable<IStringConvertible>
{
    public string ToString();
 
}

public struct GridID 
{

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

}