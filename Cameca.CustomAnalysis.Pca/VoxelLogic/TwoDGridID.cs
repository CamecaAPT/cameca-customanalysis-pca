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
    public string ToString()
    {
        return stringValue;
    }

    public (int, int) AsIndexPair()
    {
        int AAsciiValue = (int)'A';
        char firstLetter = stringValue[0];
        char secondLetter = stringValue[1];
        int firstIndex = (int)firstLetter - AAsciiValue;
        int secondIndex = (int)secondLetter - AAsciiValue;
        return (firstIndex, secondIndex);
    }

    public int CompareTo(TwoDGridID other)
    {
        return stringValue.CompareTo(other.stringValue) ;
    }
}
 
 





