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
    public string AsString()
    {
        return stringValue;
    }
    public int CompareTo(IStringConvertible other)
    {
        return stringValue.CompareTo(other.ToString());
    }
}
 
 





