using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using Prism.Regions;
using System.Runtime.CompilerServices;
using System.Windows.Documents;
using System.Security.Cryptography;
using System.Windows.Input;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Xml.Schema;
using Cameca.CustomAnalysis.Interface;
using System.Windows.Controls;

public interface IStringConvertible : IComparable<IStringConvertible>
{
    public string ToString();


    public int CompareTo(IStringConvertible other);
 
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