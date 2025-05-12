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
 
 





