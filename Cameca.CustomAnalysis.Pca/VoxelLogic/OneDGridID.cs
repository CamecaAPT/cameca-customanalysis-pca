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

public struct OneDGridID : GridID,  IComparable<GridID>
{

    public OneDGridID(int firstDim)
    {
        string firstLetter = GridID.GridLetterForIndex(firstDim);
        this.stringValue = firstLetter;
    }

}
 
 





