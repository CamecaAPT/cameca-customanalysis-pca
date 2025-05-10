using System.Collections.Generic;
using System.Linq;
using System;




public struct PcaPhaseName: IComparable<PcaPhaseName>
{
    string pcaPhase; // this string in the format A.1,B.2,C.0
    public PcaPhaseName()
    {
        this.pcaPhase = "";
    }

    public PcaPhaseName(string s)
    {
        this.pcaPhase = s;
    }

    public static PcaPhaseName InterfaceVoxelsPhaseName()
    {
        return new PcaPhaseName("Interface Voxels");
    }
    public static PcaPhaseName UnassignedVoxelsPhaseName()
    {
        return new PcaPhaseName("Unassigned Voxels");
    }

    // user displayable name removes "." and replaces "," with " "
    public string UserDisplayableName()
    {
        string shorter = pcaPhase.Replace(".", "");
        return shorter.Replace(',', ' ');
    }

    public PcaPhaseName AppendCode(string code)
    {
        if (pcaPhase.Length == 0)
        {
            pcaPhase = code;
        }
        else
        {
            pcaPhase = pcaPhase + "," + code;
        }
        return this;
    }

    public bool Contains(string s)
    {
        return pcaPhase.Contains(s);
    }

    public string[] Split(string s)
    {
        return pcaPhase.Split(s);
    }

    public int CompareTo(PcaPhaseName other)
    {
        return this.pcaPhase.CompareTo(other.pcaPhase);
    }
}

public struct PcaPhaseNameList : IComparable<PcaPhaseNameList>
{
    List<PcaPhaseName> phaseNames;
    public PcaPhaseNameList(List<PcaPhaseName> names)
    {
        List<PcaPhaseName> sortedNamesList = new List<PcaPhaseName>(names);
        sortedNamesList.Sort();
        this.phaseNames = sortedNamesList;
    }

    public string UserDisplayableName()
    {
        string displayableName = "";

        if (phaseNames.Count > 0)
        {
            displayableName = phaseNames[0].UserDisplayableName();
        }

        int numCodes = phaseNames.Count();
        for (int i = 1; i < numCodes; ++i)
        {
            displayableName += "," + phaseNames[i].UserDisplayableName();
        }
        return displayableName;
    }


    public int CompareTo(PcaPhaseNameList other)
    {
        return this.UserDisplayableName().CompareTo(other.UserDisplayableName());
    }
}




