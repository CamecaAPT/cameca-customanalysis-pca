using System.Collections.Generic;
using System.Linq;
using System;
using Cameca.CustomAnalysis.Pca;



// PhaseIDResults represents an assignment of each voxel to an integer phase.
// in the identifiedPhase Dictionary, the Key is a VoxelID, and the value is its 'phase', 
// or 0 if no phase is identified
public class PhaseIdResults
{
    public Dictionary<VoxelID, int> IdentifiedPhase;
    public Dictionary<string, TwoDPeakProjection> TwoDPeakProjections;
    public Dictionary<PcaPhaseName, int> PhaseIndexMap; // the values in identifiedPhase dictionary should correspond to the PCA codes in this list
                                           // there should be an entry "0" with the key "Unassigned Voxels"
                                           // there should be an entry N with the key "Interface Voxels" -- the index with the greatest value
                                           // So, the number of PCA Phases should be phaseIndexMap.Count - 2

    public PhaseIdResults(List<VoxelID> voxelIds)
    {
        this.IdentifiedPhase = new Dictionary<VoxelID, int>();
        // not-yet-identified voxels are identified as 0
        for (int i = 0; i < voxelIds.Count; ++i)
        {
            this.IdentifiedPhase[voxelIds[i]] = 0;
        }
        this.TwoDPeakProjections = new Dictionary<string, TwoDPeakProjection>();
        this.PhaseIndexMap = new Dictionary<PcaPhaseName, int>();
    }

    public Dictionary<int, PcaPhaseName> PhaseNamesMap() 
    {
        Dictionary<int, PcaPhaseName> namesMap = new Dictionary<int, PcaPhaseName>();
        // just reverse Keys and Values of phaseIndexMap
        foreach (KeyValuePair<PcaPhaseName, int> kvp in PhaseIndexMap)
        {
            namesMap[kvp.Value] = kvp.Key;
        }

        return namesMap;
    }

    public void SetTwoDPeakProjectionFor(string key, TwoDPeakProjection projection)
    {
        TwoDPeakProjections[key] = projection;
    }

    public void IdentifyVoxelAs(VoxelID voxelId, int phase)
    {
        IdentifiedPhase[voxelId] = phase;
    }
    public void IdentifyVoxelsAs(List<VoxelID> voxelIds, int phase)
    {
        foreach (VoxelID voxelId in voxelIds) {
            IdentifiedPhase[voxelId] = phase;
        }
    }

    public int? PhaseForVoxel(VoxelID voxelId)
    {
        if (IdentifiedPhase.ContainsKey(voxelId))
        {
            return IdentifiedPhase[voxelId];
        }
        return null;
    }

    public int? PhaseForVoxelIntValue(int voxelIndex)
    {
        VoxelID voxelId = new VoxelID(voxelIndex);
        if (IdentifiedPhase.ContainsKey(voxelId))
        {
            return IdentifiedPhase[voxelId];
        }
        return null;
    }

    public List<VoxelID> UnidentifiedVoxels()
    {
        var unidentifiedIndices = new List<VoxelID>();
        foreach ( KeyValuePair<VoxelID, int> voxel in IdentifiedPhase )
        {
            if (voxel.Value == 0) {
                unidentifiedIndices.Add(voxel.Key);
            }
        }
        return unidentifiedIndices;
    }

    public void SetPhaseIndexMap(Dictionary<PcaPhaseName, int> indexMap)
    {
        PhaseIndexMap = indexMap;
    }
}
    
  