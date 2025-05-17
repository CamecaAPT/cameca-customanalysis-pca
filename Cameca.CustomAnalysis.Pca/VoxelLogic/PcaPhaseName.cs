using System.Collections.Generic;
using System.Linq;
using System;

namespace Cameca.CustomAnalysis.Pca.VoxelLogic;

public readonly struct PcaPhaseName: IComparable<PcaPhaseName>, IEquatable<PcaPhaseName>
{
    readonly string pcaPhase; // this string in the format A.1,B.2,C.0
    public PcaPhaseName()
    {
        pcaPhase = "";
    }

    public PcaPhaseName(string s)
    {
        pcaPhase = s;
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
    public readonly string UserDisplayableName()
    {
        string shorter = pcaPhase.Replace(".", "");
        return shorter.Replace(',', ' ');
    }
    
    public readonly List<string> PhaseComponents()
    {
        return pcaPhase.Split(",").ToList();
    }

    public PcaPhaseName AppendCode(string code)
    {
        string pca;
        if (pcaPhase.Length == 0)
        {
            pca = code;
        }
        else
        {
            pca = pcaPhase + "," + code;
        }
        return new PcaPhaseName(pca);
    }

    public readonly bool Contains(string s)
    {
        return pcaPhase.Contains(s);
    }

    public readonly string[] Split(string s)
    {
        return pcaPhase.Split(s);
    }

    public readonly int CompareTo(PcaPhaseName other)
    {
        return this.pcaPhase.CompareTo(other.pcaPhase);
    }

    public readonly bool Equals(PcaPhaseName other)
    {
        return pcaPhase == other.pcaPhase;
    }
}

public readonly struct PcaPhaseNameList : IComparable<PcaPhaseNameList>
{
    readonly List<PcaPhaseName> phaseNames;
    public PcaPhaseNameList(List<PcaPhaseName> names)
    {
        List<PcaPhaseName> sortedNamesList = new(names);
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

        int numCodes = phaseNames.Count;
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




