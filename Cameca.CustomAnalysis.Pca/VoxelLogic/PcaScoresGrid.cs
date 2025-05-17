
using PcaExtensionMethods;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

using Cameca.CustomAnalysis.Pca.Models;
using System.Windows.Shapes;
namespace Cameca.CustomAnalysis.Pca.VoxelLogic;

// PcaScoresGrid represents a three dimensional grid containing the PCA scores for a collection of voxels
// PcaScoresGrid is initialized with the size of the grid in x y and z, so that it can then map 
// a particular [x,y,z] grid coordinate to an index (and back) 
//
// Of particular interest to the PCA data processing logic, this class implements the logic for 
// calculating PhaseIdResults -- using the PCA score data to identify which voxels belong to which 
// 'phases'

public interface IScoresProvider
{
    public (VoxelID, float[]) GetIDAndScores(int voxelIndex);
}
public class PcaScoresGrid
{
    readonly Dictionary<VoxelID, PcaVoxel> pcaVoxels; // the key is the 'id' for the voxel, from which you can
                                             // calculate the x,y,z position of the voxel on the grid if
                                             // the grid dimensions are known

    readonly Dictionary<TwoDGridID, List<List<PixelID>>> twoDPartitions; // key is grid ID
          // value is lists of associations of voxelID with different peaks

    readonly Dictionary<TwoDGridID, DensityPlane> twoDGrids;  // key is grid ID

    readonly Dictionary<OneDGridID, List<List<BinID>>> oneDPartitions; // key is grid ID
          // value is lists of associations of binID with different partitions

    readonly Dictionary<OneDGridID, DensityLine> oneDGrids;  // key is grid ID

    readonly int scoreDims;
    ThreeDGridDimensions gridDims;

    public VoxelID VoxelIDFor(ThreeDGridCoord gridCoord)
    {
        return gridCoord.VoxelIdFor(gridDims);
    }

    public ThreeDGridCoord GridCoordFor(VoxelID voxelId)
    {
        return voxelId.GridCoordFor(gridDims);
    }

    // really just the ASCII value of the first character
    static internal int ASCIIValueForChar(char s)
    {
        return (int)s;
    }

    static internal char CharValueForASCII(int a)
    {
        return (char)a;
    }
    // returns a list of 27 indices, unless the voxel is near the edge
    // note: also includes self
    List<VoxelID> NeighborIDsFor(ThreeDGridCoord gridCoord)
    {
        List<VoxelID> neighborIndices = new();

        int minx = Math.Max(0, gridCoord.x - 1);
        int maxx = Math.Min(gridDims.x - 1, gridCoord.x + 1);
        int miny = Math.Max(0, gridCoord.y - 1);
        int maxy = Math.Min(gridDims.y - 1, gridCoord.y + 1);
        int minz = Math.Max(0, gridCoord.z - 1);
        int maxz = Math.Min(gridDims.z - 1, gridCoord.z + 1);
        for (int z = minz; z <= maxz; ++z)
        {
            for (int y = miny; y <= maxy; ++y)
            {
                for (int x = minx; x <= maxx; ++x)
                {
                    VoxelID voxelId = new(x + (y * gridDims.x) + (z * gridDims.xy));
                    neighborIndices.Add(voxelId);
                }
            }
        }
        return neighborIndices;
    }

    public PcaScoresGrid(IScoresProvider scoresProvider, int nIndices, int scoreDimensions, ThreeDGridDimensions gridDimensions)
    {
        scoreDims = scoreDimensions;

        pcaVoxels = PcaVoxelsInit(scoresProvider, nIndices);

        gridDims = gridDimensions;
        twoDPartitions = new Dictionary<TwoDGridID, List<List<PixelID>>>();
        twoDGrids = new Dictionary<TwoDGridID, DensityPlane>();
        oneDPartitions = new Dictionary<OneDGridID, List<List<BinID>>>();
        oneDGrids = new Dictionary<OneDGridID, DensityLine>();
    }

    static Dictionary<VoxelID, PcaVoxel> PcaVoxelsInit(IScoresProvider scoresProvider, int nIndices)
    {
        var voxels = new Dictionary<VoxelID, PcaVoxel>();

        for (int i = 0; i < nIndices; ++i)
        {
            float[] nthVector;
            VoxelID voxelId;

            (voxelId, nthVector) = scoresProvider.GetIDAndScores(i);

            var pcaVoxel = new PcaVoxel(voxelId, nthVector);
            voxels[voxelId] = pcaVoxel;
        }

        return voxels;
    }

    // When assigning each voxel to a phase based on which peak it might be in,
    // We want to avoid mapping each pixel to a peak more than once
    // SO, first  put all the voxels into a different list corresponding to each pixel
    // then we can do the lookup once and assign all the voxels from that pixel to 
    // the same phase
    static Dictionary<PixelID, List<VoxelID>> AggregateVoxelsIntoListsPerPixel(List<VoxelID> voxelIds, DensityPlane grid, Dictionary<VoxelID, PcaVoxel> pcaVoxels, int firstDim, int secondDim)
    {
        // For each voxel, assign the voxel to the List corresponding with its pixel
        Dictionary<PixelID, List<VoxelID>> voxelLists = new();
        foreach (VoxelID voxelId in voxelIds)
        {
            PcaVoxel voxel = pcaVoxels[voxelId];
            PixelID? nthPixelId = grid.PixelIDFor(voxel.scoreVector[firstDim], voxel.scoreVector[secondDim]);
            if (nthPixelId != null)
            {
                PixelID pixelID = nthPixelId.Value;
                List<VoxelID>? voxelIDListForGridCoord;
                if (voxelLists.TryGetValue(pixelID, out voxelIDListForGridCoord))
                {
                    voxelIDListForGridCoord.Add(voxelId);
                }
                else
                {
                    voxelIDListForGridCoord = new List<VoxelID>();
                    voxelIDListForGridCoord.Add(voxelId);
                    voxelLists[pixelID] = voxelIDListForGridCoord;
                }
            }
        }
        return voxelLists;
    }

    static Dictionary<BinID, List<VoxelID>> AggregateVoxelsIntoListsPerBin(List<VoxelID> voxelIds, DensityLine line, Dictionary<VoxelID, PcaVoxel> pcaVoxels, int pcaDimension)
    {
        // For each voxel, assign the voxel to the List corresponding with its bin
        Dictionary<BinID, List<VoxelID>> voxelLists = new();
        foreach (VoxelID voxelId in voxelIds)
        {
            PcaVoxel voxel = pcaVoxels[voxelId];
            BinID? nthBinId = line.BinIDFor(voxel.scoreVector[pcaDimension]);
            if (nthBinId != null)
            {
                BinID binID = nthBinId.Value;
                List<VoxelID>? voxelIDListForBin;
                if (voxelLists.TryGetValue(binID, out voxelIDListForBin))
                {
                    voxelIDListForBin.Add(voxelId);
                }
                else
                {
                    voxelIDListForBin = new List<VoxelID>();
                    voxelIDListForBin.Add(voxelId);
                    voxelLists[binID] = voxelIDListForBin;
                }
            }
        }
        return voxelLists;
    }
    // VoxelBuckets essentially inverts the dictionary passed in
    // returns a dictionary where the key is the pcaCode string,
    // and the value is a HashSet of VoxelIDs with that pcaCode
    static internal Dictionary<PcaPhaseName, HashSet<VoxelID>> VoxelBuckets(Dictionary<VoxelID, PcaPhaseName> pcaCodes)
    {
        Dictionary<PcaPhaseName, HashSet<VoxelID>> buckets = new();
        foreach (KeyValuePair<VoxelID, PcaPhaseName> kvp in pcaCodes)
        {
            if (buckets.ContainsKey(kvp.Value))
            {
                buckets[kvp.Value].Add(kvp.Key);
            }
            else
            {
                HashSet<VoxelID> newSet = new();
                newSet.Add(kvp.Key);
                buckets[kvp.Value] = newSet;
            }
        }
        return buckets;
    }
    static internal Dictionary<PcaPhaseName, int> PcaCodeCounts(Dictionary<PcaPhaseName, HashSet<VoxelID>> voxelBuckets)
    {
        Dictionary<PcaPhaseName, int> pcaCodeCounts = new();
        foreach (KeyValuePair<PcaPhaseName, HashSet<VoxelID>> kvp in voxelBuckets)
        {
            pcaCodeCounts[kvp.Key] = kvp.Value.Count;
        }
        return pcaCodeCounts;
    }
    static internal void DumpPcaCodeCounts(Dictionary<string, int> pcaCodeCounts)
    {
        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string outputFilename = System.IO.Path.Combine(docPath, "PcaCodeStats.txt");

        using (StreamWriter outputFile = new(outputFilename))
        {
            List<string> sortedCodes = pcaCodeCounts.Keys.ToList();
            sortedCodes.Sort();
            outputFile.WriteLine("PcaCodeStats file");
            foreach (string pcaCode in sortedCodes)
            {
                int count = pcaCodeCounts[pcaCode];
                outputFile.WriteLine("Code: \t" + pcaCode + "\t Count:\t" + count);
            }
            outputFile.Close();
        }
    }

    static internal void DumpPartitions(List<List<PixelID>> partitions, string gridId)
    {
        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string outputFilename = System.IO.Path.Combine(docPath, "PcaPeakPartitions" + gridId + ".txt");
        // int peakIndex = 1;
        using (StreamWriter outputFile = new(outputFilename))
        {
            outputFile.Write("peakCoords = {");
            bool firstPeak = true;
            foreach (List<PixelID> partition in partitions)
            {
                if (!firstPeak)
                {
                    outputFile.Write(",");
                }
                else
                {
                    firstPeak = false;
                }
                outputFile.Write("{");
                bool firstCoord = true;
                foreach (PixelID pixelId in partition)
                {
                    (int x, int y) = pixelId.XYCoords();
                    if (!firstCoord) {
                        outputFile.Write(",");
                    }
                    else
                    {
                        firstCoord = false;
                    }
                    outputFile.Write("{" + x + "," + y + "}");
                }
                outputFile.WriteLine("}¨\n");
                //  peakIndex += 1;
            }

            outputFile.Write("}");
            outputFile.Close();
        }
    }

    static internal List<VoxelID> FindMatchingSets(VoxelID neighborID, Dictionary<VoxelID, HashSet<VoxelID>> voxelSets)
    {
        List<VoxelID> matchingSets = new();
        foreach (KeyValuePair<VoxelID, HashSet<VoxelID>> kvp in voxelSets)
        {
            if (kvp.Value.Contains(neighborID))
            {
                matchingSets.Add(kvp.Key);
            }
        }
        return matchingSets;
    }

    // FilterForMatchableCode identifies which of the pcaCodes in nonZeroPcaCodes are
    // 'one-away' matches for pcaCode
    // The reason we use a . to separate the letter from the number in PCA codes is to 
    // make it easy to identify a zero -- i.e. the algorithm here also
    // works if there are 10 peaks, because "A.10" doesn't contain ".0"
    static internal HashSet<PcaPhaseName> FilterForMatchableCode(List<PcaPhaseName> pcaCodesToMatch, PcaPhaseName pcaCode)
    {
        // if pcaCode contains zero 0s or more than one 0, return empty List
        HashSet<PcaPhaseName> matches = new();

        // tokens here means 'substrings' -- parts of the PCA code that should be matched
        string[] tokens = pcaCode.Split(".0");
        int numTokens = tokens.Length;
        if (numTokens > 1)
        {
            foreach (PcaPhaseName candidate in pcaCodesToMatch)
            {
                bool matchesAll = true;
                int lastTokenIndex = numTokens - 1;
                for (int t = 0; t < lastTokenIndex; t += 1)
                {
                    if (!candidate.Contains(tokens[t]))
                    {
                        matchesAll = false;
                    }
                }

                // the last token might be an emptyString
                string lastToken = tokens[lastTokenIndex];
                if ((lastToken.Length > 0) && (!candidate.Contains(lastToken)))
                {
                    matchesAll = false;
                }

                if (matchesAll)
                {
                    matches.Add(candidate);
                }
            }
        }

        return matches;
    }

    static void AddVoxelsToHashSetDictionary(Dictionary<PcaPhaseName, HashSet<VoxelID>> additions, Dictionary<PcaPhaseName, HashSet<VoxelID>> sets)
    {
        foreach (KeyValuePair<PcaPhaseName, HashSet<VoxelID>> kvp in additions)
        {
            HashSet<VoxelID> hashSet;
            if (sets.ContainsKey(kvp.Key))
            {
                hashSet = sets[kvp.Key];
            }
            else
            {
                hashSet = new HashSet<VoxelID>();
            }
            foreach (VoxelID voxelId in kvp.Value)
            {
                hashSet.Add(voxelId);
            }
            sets[kvp.Key] = hashSet;
        }
    }

    static void AddVoxelsToPcaPhaseNameListSet(Dictionary<PcaPhaseNameList, HashSet<VoxelID>> additions, Dictionary<PcaPhaseNameList, HashSet<VoxelID>> sets)
    {
        foreach (KeyValuePair<PcaPhaseNameList, HashSet<VoxelID>> kvp in additions)
        {
            HashSet<VoxelID> hashSet;
            if (sets.ContainsKey(kvp.Key))
            {
                hashSet = sets[kvp.Key];
            }
            else
            {
                hashSet = new HashSet<VoxelID>();
            }
            foreach (VoxelID voxelId in kvp.Value)
            {
                hashSet.Add(voxelId);
            }
            sets[kvp.Key] = hashSet;
        }
    }

    // return the total number of voxels unassigned
    static int SubtractVoxelsFromHashSetDictionary(Dictionary<PcaPhaseName, HashSet<VoxelID>> subtractions, Dictionary<PcaPhaseName, HashSet<VoxelID>> sets)
    {
        int unassignedCount = 0;
        foreach (KeyValuePair<PcaPhaseName, HashSet<VoxelID>> kvp in subtractions)
        {
 
            if (sets.ContainsKey(kvp.Key))
            {

                HashSet<VoxelID> hashSet = sets[kvp.Key];
                int countBeforeRemoval = hashSet.Count;

                foreach (VoxelID voxelId in kvp.Value)
                {
                    hashSet.Remove(voxelId);
                }
                int subtractionsCount = kvp.Value.Count;
                int countAfterRemoval = hashSet.Count;
                int actuallyRemoved = countBeforeRemoval - countAfterRemoval;
                if (actuallyRemoved != subtractionsCount)
                {
                    throw new Exception("SubtractVoxels expected all voxels to exist in previous bucket");
                }
                unassignedCount += actuallyRemoved;
                sets[kvp.Key] = hashSet;
            }
            else
            {
                throw new Exception("SubtractVoxels expected existing bucket of voxels");
            }
        }
        return unassignedCount;
    }

    public OneDGridsResults CalculateOneDGrids(PcaPhaseIdentificationProperties properties)
    {
        OneDGridsResults gridsResults = new();
        oneDGrids.Clear();
        oneDPartitions.Clear();

        List<VoxelID> voxelIds = pcaVoxels.Keys.ToList();

        float binSeparation = properties.oneDProjectionBinSize; 
        float delocalization = properties.oneDProjectionDelocalization;
        int numDimsToInclude = this.scoreDims;
 
        // now, make a oneD grid for each dimensions
        for (int i = 0; i < numDimsToInclude; ++i)
        {
            OneDGridID gridId = new(i);

            var oneDGrid = CalculateOneDDensity(voxelIds, i, binSeparation, delocalization);
            oneDGrids[gridId] = oneDGrid;

            // this identifies the main peak and other regions
            List<List<BinID>> partitionedIndices = IdentifyOneDPartitions(oneDGrid, i, properties);
            oneDPartitions[gridId] = partitionedIndices;

            var projection = new OneDPeakProjection(oneDGrid);
            gridsResults.SetOneDPeakProjectionFor(gridId, projection);
            oneDPartitions[gridId] = partitionedIndices;
        }
        return gridsResults;
    }

    public TwoDGridsResults CalculateTwoDGrids(PcaPhaseIdentificationProperties properties)
    {
        TwoDGridsResults gridsResults = new();
        twoDGrids.Clear();
        twoDPartitions.Clear();

        List<VoxelID> voxelIds = pcaVoxels.Keys.ToList();

        float binSeparation = properties.gridProjectionBinSize;
        float delocalization = properties.gridProjectionDelocalization;
        int numDimsToInclude = this.scoreDims;

        // now, make twoD grids using all pairs of dimensions
        for (int i = 0; i < (numDimsToInclude - 1); ++i)
        {
            for (int j = i + 1; j < numDimsToInclude; ++j)
            {
                TwoDGridID gridId = new(i, j);

                // this makes the 2D grid  --  step A) above
                var twoDGrid = CalculateTwoDDensity(voxelIds, i, j, binSeparation, delocalization);
                twoDGrids[gridId] = twoDGrid;

                // this identifies the peaks --  step B) above
                List<List<PixelID>> partitionedIndices = twoDGrid.IdentifyTwoDPartitions(properties);
                twoDPartitions[gridId] = partitionedIndices;

                var projection = new TwoDPeakProjection(twoDGrid);
                gridsResults.SetTwoDPeakProjectionFor(gridId, projection);
                twoDPartitions[gridId] = partitionedIndices;

                // Enable this to get a text file with partition data
                // DumpPartitions(partitionedIndices, gridId.ToString());     
            }
        }
        return gridsResults;
    }

    // GetPhasesStrategyF is produces PhaseIdResults based on the first three 
    // PCA dimensions only  
    //
    // The strategy goes like this:
    //
    // A) make three 2D grids representing the density of voxels with PCA scores in 
    //    each combination of the first three PCA dimensions 
    //    in other words dim1xdim2,  dim1xdim3,  dim2xdim3
    // B) identify peaks in each 2D grid -- each peak corresponds to a section of a PCA code
    //      A1 is a code snippet representing the first peak of the first grid
    //      A2 is a code snippet representing the second peak of the first grid  
    //      B1 is a code snippet representing the first peak of the second grid    
    //      B0 is a code snippet representing no association with any peak of the second grid
    //      etc.
    // C) look at all of the PCA codes of the form
    //      AhBkCl
    //    where h, k, and l are non-zero.  Regions of contiguous voxels that share 
    //    the same code will be designated as the same phase 
    // D) add voxels that abut any of the contiguous regions to those regions iff
    //    the code they have matches all code snippet for which they have a non-zero number
    //    i.e. A0B1C1  is a match for contiguous regions A1B1C1 and A2B1C1
    public PhaseIdResults GetPhasesStrategyF(PcaPhaseIdentificationProperties properties, HashSet<string> twoDGridsToInclude, HashSet<string> oneDGridsToInclude)
    {
        List<VoxelID> voxelIds = pcaVoxels.Keys.ToList();
        int numDimsToInclude = Math.Min(properties.numDimsForPCAPhaseId, this.scoreDims);

        // make a dictionary for the pcaCodes and fill with empty Strings
        Dictionary<VoxelID, PcaPhaseName> pcaCodes = new();
        foreach (VoxelID voxelId in voxelIds)
        {
            pcaCodes[voxelId] = new PcaPhaseName();
        }
        // starting here, loop through the grids and if we have selected 
        // the grid to be used in PCA phase Identification,
        // get the grid ID and assign pca codes to all the voxels based on 
        // which peak it is part of in the grid
        foreach (KeyValuePair<TwoDGridID, List<List<PixelID>>> kvp in twoDPartitions)
        {
            // now label each voxel with a PCA code based on its peak association
            // for each grid, group the voxels into lists per pixel, then, knowing
            // which peaks contain which pixels, add the voxels PCA code for that grid to its entry in 
            // the PCA code dictionary

            TwoDGridID gridID = kvp.Key;
            if (twoDGridsToInclude.Contains(gridID.ToString()))
            {
                DensityPlane twoDGrid = twoDGrids[gridID];
                List<List<PixelID>> partitionedIndices = kvp.Value;
                (int firstDim, int secondDim) = gridID.AsIndexPair();

                Dictionary<PixelID, List<VoxelID>> voxelLists = AggregateVoxelsIntoListsPerPixel(voxelIds, twoDGrid, pcaVoxels, firstDim, secondDim);
                int peakIndex = 1;

                foreach (List<PixelID> pixelIdList in partitionedIndices)
                {
                    string pcaCode = gridID.ToString() + "." + peakIndex.ToString();
                    // the pixelIdList contains a list of pixelIds identified as being part of the Nth partition
                    foreach (PixelID pixelID in pixelIdList)
                    {
                        // Lookup for all the voxels bucketed under this pixelId
                        if (voxelLists.ContainsKey(pixelID))
                        {
                            List<VoxelID> voxelIdsForThisPixel = voxelLists[pixelID];
                            foreach (VoxelID voxelId in voxelIdsForThisPixel)
                            {
                                pcaCodes[voxelId] = pcaCodes[voxelId].AppendCode(pcaCode);
                            }
                            // remove that entry from voxelLists
                            voxelLists.Remove(pixelID);
                        }
                    }
                    peakIndex += 1;
                }

                // now, all the remaining entries in voxelLists are unassigned :  
                // assign these to component 0
                string unassignedPcaCode = gridID.ToString() + ".0";
                List<PixelID> unassignedPixels = voxelLists.Keys.ToList();

                foreach (PixelID pixelID in unassignedPixels)
                {
                    // Lookup for all the voxels bucketed under this pixelId
                    List<VoxelID> voxelIdsForThisPixel = voxelLists[pixelID];
                    foreach (VoxelID voxelId in voxelIdsForThisPixel)
                    {
                        pcaCodes[voxelId] = pcaCodes[voxelId].AppendCode(unassignedPcaCode);
                    }
                }
            }
        }

        // now, add the oneD grids
        foreach (string s in oneDGridsToInclude)
        {
            OneDGridID gridId = new(s);
            DensityLine oneDGrid = oneDGrids[gridId];
            List<List<BinID>> partitionedIndices = oneDPartitions[gridId];
            int pcaDimension = gridId.PCAIndex();
            Dictionary<BinID, List<VoxelID>> voxelLists = AggregateVoxelsIntoListsPerBin(voxelIds, oneDGrid, pcaVoxels, pcaDimension);
            int peakIndex = 1; 
            foreach (List<BinID> binIdList in partitionedIndices)
            {
                string pcaCode = gridId.ToString() + "." + peakIndex.ToString();
                // the pixelIdList contains a list of pixelIds identified as being part of the Nth partition
                foreach (BinID binId in binIdList)
                {
                    // Lookup for all the voxels bucketed under this binId
                    if (voxelLists.ContainsKey(binId))
                    {
                        List<VoxelID> voxelIdsForThisBin = voxelLists[binId];
                        foreach (VoxelID voxelId in voxelIdsForThisBin)
                        {
                            pcaCodes[voxelId] = pcaCodes[voxelId].AppendCode(pcaCode);
                        }
                        // remove that entry from voxelLists
                        voxelLists.Remove(binId);
                    }
                }
                peakIndex += 1;
            }
            // now, all the remaining entries in voxelLists are unassigned :  
            // assign these to component 0
            string unassignedPcaCode = gridId.ToString() + ".0";
            List<BinID> unassignedBins = voxelLists.Keys.ToList();

            foreach (BinID binID in unassignedBins)
            {
                // Lookup for all the voxels bucketed under this pixelId
                List<VoxelID> voxelIdsForThisBin = voxelLists[binID];
                foreach (VoxelID voxelId in voxelIdsForThisBin)
                {
                    pcaCodes[voxelId] = pcaCodes[voxelId].AppendCode(unassignedPcaCode);
                }
            }
        }

        // PcaStream writes a file with text data about the progression of the algorithm
        // uncomment it here and uncomment the calls to DumpVoxelSetStats below
        // PcaStream pcaStream = new PcaStream("GetPhasesStrategyF");
        PhaseIdResults phaseIdResults = new(voxelIds);

        // now examine the groups of voxels to identify contiguous regions
        // in this case, 'unassigned' means voxels not yet associated with a list of voxels in a pcaPhase
        Dictionary<PcaPhaseName, HashSet<VoxelID>> unassignedVoxelBuckets = VoxelBuckets(pcaCodes);

        // first, lets dump some info about populations of all the different 
        // pca Codes:
        Dictionary<PcaPhaseName, int> pcaCodeCounts = PcaCodeCounts(unassignedVoxelBuckets);

        // Enable this line to get a text file with counts of the different buckets
        //DumpPcaCodeCounts(pcaCodeCounts);

        // At this stage we want to identify which voxels are part of contiguous regions of phases
        // At first, assume that all PCA codes that have no zero value in them identify a particular phase
        // So, generate a list of voxels for each of these phases 
        List<PcaPhaseName> nonZeroPcaCodes = unassignedVoxelBuckets.Keys.Where(code => !code.Contains(".0")).ToList();
        Dictionary<PcaPhaseName, HashSet<VoxelID>> pcaCodeVoxelSets = new();
        Dictionary<PcaPhaseNameList, HashSet<VoxelID>> interfaceVoxelSets = new();

        // pcaCodes is a Dictionary<VoxelID, string>
        foreach (PcaPhaseName pcaCode in nonZeroPcaCodes)
        {
            // just copy over the voxel List to be the base list for that phase
            pcaCodeVoxelSets[pcaCode] = unassignedVoxelBuckets[pcaCode];
            unassignedVoxelBuckets.Remove(pcaCode);
        }

        // Now, it is very possible that there are some identified partitions 
        // that are not represented in the complete set of nonZeroPcaCodes
        // that is, there is a partition identified in one of the grids that 
        // is not part of any of the nonZeroPcaCodes.  If this is the case,
        // we need to allow for a phase to be defined with at least one zero in it.
        // So, get the remaining PCA codes for the voxels.
        // identify any parts of them not in the other sets
        List<PcaPhaseName> otherPcaCodes = unassignedVoxelBuckets.Keys.ToList();
        HashSet<string> codeComponents = new();
        otherPcaCodes.ForEach(pcaCode =>
        {
            pcaCode.PhaseComponents().ForEach(component =>
            {
                if (!component.Contains('0'))
                {
                    codeComponents.Add(component);
                }
            });
        });
        // remove components represented in nonZeroPcaCodes:
        nonZeroPcaCodes.ForEach(pcaCode =>
        {
            pcaCode.PhaseComponents().ForEach(component =>
            {
                codeComponents.Remove(component);
            });
        });
        foreach(string component in codeComponents) 
        {
            otherPcaCodes.ForEach(code =>
            {
                // need to check if unassignedVoxelBuckets contains the code
                // because we might hit the same code twice
                if (code.Contains(component) && unassignedVoxelBuckets.ContainsKey(code))
                {
                    pcaCodeVoxelSets[code] = unassignedVoxelBuckets[code];
                    unassignedVoxelBuckets.Remove(code);
                }
            });
        }
        // now pcaCodeVoxelSets is filled with data for each pcaCode,
        // These are our 'core' regions
        // now lets go through the voxels with PcaCodes that contain a 0
        // and see if they might be adjacent to voxels in any of the core regions  
        // where that 0 is non-zero
        // three possibilities:
        //   1) not adjacent to any
        //   2) adjacent to voxels sharing a single pcaCode  ( i.e. A1B0C1 adjacent to A1B1C1)
        //   3) adjacent to voxels of multiple pcaCodes  ( i.e. A1B0C1 adjacent to both A1B1C1 and A1B2C1 voxels)
        //
        // for case 1) do nothing
        // for case 2) add the voxels to the core regions
        // for case 3) designate the voxel as an interface voxel

        List<PcaPhaseName> assignedPhaseNames = pcaCodeVoxelSets.Keys.ToList();
        int voxelAssignmentCount = 1;
        while (voxelAssignmentCount > 0)
        {
            Dictionary<PcaPhaseName, HashSet<VoxelID>> unassignedSubtractions = new();
            Dictionary<PcaPhaseName, HashSet<VoxelID>> voxelSetAdditions = new();
            Dictionary<PcaPhaseNameList, HashSet<VoxelID>> interfaceVoxelSetAdditions = new();

            // this loop initializes the lists of voxels to add for each PCA code
            int numAssignedPhaseNames = assignedPhaseNames.Count;
            for (int nOuter = 0; nOuter < numAssignedPhaseNames; ++nOuter)
            {
                PcaPhaseName nonZeroPcaCode = assignedPhaseNames[nOuter];
                voxelSetAdditions[nonZeroPcaCode] = new HashSet<VoxelID>();
            }

            List<PcaPhaseName> unassignedPhaseNames = unassignedVoxelBuckets.Keys.ToList();
            foreach (PcaPhaseName phaseName in unassignedPhaseNames)
            {
                HashSet<PcaPhaseName> matchableCodes = FilterForMatchableCode(assignedPhaseNames, phaseName);
                // matchableCodes are the phaseNames that are "one away" from the pcaCode under consideration

                HashSet<VoxelID> potentialAdditions = unassignedVoxelBuckets[phaseName];
                HashSet<VoxelID> subtractions = new();
                List<PcaPhaseName> matches = new();
                foreach (VoxelID voxelId in potentialAdditions)
                {
                    List<VoxelID> neighbors = voxelId.NeighborVoxels(gridDims);
                    matches.Clear();

                    foreach (PcaPhaseName matchingCode in matchableCodes)
                    {
                        if (pcaCodeVoxelSets[matchingCode].ContainsAny(neighbors))
                        {
                            matches.Add(matchingCode);
                        }
                    }

                    // case 1 is no matches 
                    if (matches.Count > 0)
                    {
                        if (matches.Count == 1)
                        {
                            voxelSetAdditions[matches[0]].Add(voxelId);
                            subtractions.Add(voxelId);
                        }
                        else
                        {
                            PcaPhaseNameList interfaceId = new(matches);
                            if (interfaceVoxelSetAdditions.ContainsKey(interfaceId))
                            {
                                interfaceVoxelSetAdditions[interfaceId].Add(voxelId);
                            }
                            else
                            {
                                HashSet<VoxelID> newSet = new();
                                newSet.Add(voxelId);
                                interfaceVoxelSetAdditions[interfaceId] = newSet;
                            }
                            subtractions.Add(voxelId);
                        }
                    }
                }
                unassignedSubtractions[phaseName] = subtractions;
            }
            // now, any voxel that is part of interfaceVoxelSetAdditions
            // or voxelSetAdditions should get taken out of unassignedVoxelBuckets
            // and added to interfaceVoxelSets or pcaCodeVoxelSets

            // calls to DumpVoxelSetStats are used to follow the progression of the algorithm is assigning voxels
            // these can be enabled if the pcaStream object is created above

            // pcaStream.WriteTimestamp("finished voxel partitioning" );
            // pcaStream.DumpVoxelSetStats("start pcaCodeVoxelSets", pcaCodeVoxelSets); 
            // pcaStream.DumpVoxelSetStats("start pcaCodeVoxelSets", pcaCodeVoxelSets);
            // pcaStream.DumpVoxelSetStats("startUnassigned", unassignedVoxelBuckets);
            // pcaStream.DumpVoxelSetStats("voxelSetAdditions", voxelSetAdditions);
            // pcaStream.DumpVoxelSetStats("interfaceVoxelSetAdditions", interfaceVoxelSetAdditions);

            AddVoxelsToPcaPhaseNameListSet(interfaceVoxelSetAdditions, interfaceVoxelSets);
            AddVoxelsToHashSetDictionary(voxelSetAdditions, pcaCodeVoxelSets);
            voxelAssignmentCount = SubtractVoxelsFromHashSetDictionary(unassignedSubtractions, unassignedVoxelBuckets);

            // pcaStream.DumpVoxelSetStats("assignments sources", unassignedSubtractions);
            // pcaStream.DumpVoxelSetStats("new pcaCodeVoxelSets", pcaCodeVoxelSets);
            // pcaStream.DumpVoxelSetStats("stillUnassigned", unassignedVoxelBuckets);
            // pcaStream.WriteTimestamp("assigned " + voxelAssignmentCount + " voxels");
        }

        // now, pcaCodeVoxelSets is ready to be used for define a per-voxel component mapping
        // lets order the pcaCodeVoxelSets by population
        List<PcaPhaseName> pcaCodesByPopulation = pcaCodeVoxelSets.Keys.ToList();
        var comparator = new PopulationSorter<PcaPhaseName, VoxelID>(pcaCodeVoxelSets);
        pcaCodesByPopulation.Sort(comparator);

        // now pcaCodesByPopulation is sorted?
        // make a pcaCode to phaseIndex map:
        // 
        Dictionary<PcaPhaseName, int> phaseIndexMap = new();
        PcaPhaseName unassignedVoxelsPhaseName = PcaPhaseName.UnassignedVoxelsPhaseName();
        PcaPhaseName interfaceVoxelsPhaseName = PcaPhaseName.InterfaceVoxelsPhaseName();
        phaseIndexMap[unassignedVoxelsPhaseName] = 0;
        int phaseIndex = 1;
        foreach (PcaPhaseName pcaCode in pcaCodesByPopulation)
        {
            phaseIndexMap[pcaCode] = phaseIndex;
            phaseIndex += 1;
        }
        phaseIndexMap[interfaceVoxelsPhaseName] = phaseIndex;

        // and, call IdentifyVoxelAs for each voxel
        // phaseResults initializes phase ID to zero for each voxel
        // So, we only need to assign voxels that have landed in the pcaCodeVoxelSets
        foreach (VoxelID voxelId in voxelIds)
        {
            phaseIdResults.IdentifyVoxelAs(voxelId, 0);
        }
        foreach (KeyValuePair<PcaPhaseName, HashSet<VoxelID>> kvp in pcaCodeVoxelSets)
        {
            PcaPhaseName nthPcaCode = kvp.Key;

            if (phaseIndexMap.ContainsKey(nthPcaCode))
            {
                int voxelphase = phaseIndexMap[nthPcaCode];

                foreach (VoxelID voxelId in kvp.Value)
                {
                    phaseIdResults.IdentifyVoxelAs(voxelId, voxelphase);
                }
            }
        }
        phaseIdResults.SetPhaseIndexMap(phaseIndexMap);

        // for exery interface voxel, set it to type 5:
        //  Dictionary<string, HashSet<VoxelID>> interfaceVoxelSets = new Dictionary<string, HashSet<VoxelID>>();
        foreach (KeyValuePair<PcaPhaseNameList, HashSet<VoxelID>> kvp in interfaceVoxelSets)
        {
            int interfaceVoxelBucket = phaseIndexMap[interfaceVoxelsPhaseName];
            foreach (VoxelID voxelId in kvp.Value)
            {
                phaseIdResults.IdentifyVoxelAs(voxelId, interfaceVoxelBucket);
            }
        }

        // pcaStream.Close();
        return phaseIdResults;
    }


    // IdentifyOneDPartitions use the projection from the oneDGrid to separate 
    // voxels that belong to different parts in the Density Line
    // typically, this is used for the case where there is only one identifiable peak in the 
    // projection -- the partitioning is done into voxels that land inside the peak (1), 
    // and voxels that land well away from the peak (2).  An interface region (0) is defined
    // for voxels that are close to the peak, but not inside it.
    // then, for each voxel, see if it lands in on of the partitioned bins
    // If it does, add it to the appropriate list
    // return the list of lists
    // indices not identified are not returned in any list
    static public List<List<BinID>> IdentifyOneDPartitions(DensityLine oneDGrid, int dimX, PcaPhaseIdentificationProperties props)
    {
        // to identify the first maximum, just find the pixel with the highest value
        // then, accumulate neighboring pixels, avoiding neighbors with higher values
        // accumulate neighbors in order of their density value.
        // stop accumulating when a slope increase is found:
        //   this indicates there must be another maximum to look for
        // also, stop at 10% of peak max
        // to look for another maximum, find the remaining pixel with the maximum value.
        // continue accumulating pixels into all peaks

        // partitionFinder operates on the grid, identifying pixels
        // associated with the different maxima
        OneDGridPartitionFinder partitionFinder = new(oneDGrid, props);

        partitionFinder.FindPartitions();
        var binLists = partitionFinder.GetBinLists();
        partitionFinder.Clear();
        return binLists;
    }


    private static double GaussianOfXWithInverseG(float x, float inverseg)
    {
        return (inverseg * Math.Exp(-Math.PI * x * x * inverseg * inverseg));
    }

    // The coefficients for positive values (i.e. all but the first one at index zero)
    // are also used for the negative values, and the area under the curve should be equal to one.
    // so, add up all the values and adjust them all so that the total is one.
    private static List<float> NormalizeCoefficients(List<double> coeffs)
    {
        int limit = coeffs.Count - 1;
        List<float> normalized = new();
        if (limit >= 0)
        {
            double sum = 0.0;
            for (int h = -limit; h <= limit; ++h)
            {
                int indexInArray = (h < 0) ? -h : h;
                sum += coeffs[indexInArray];
            }
            double correction = (1.0 / sum);
            for (int h = 0; h <= limit; ++h)
            {
                normalized.Add((float)(coeffs[h] * correction));
            }
        }
        return normalized;
    }

    // In the case of a small extra delocalization, a gaussian doesn't capture
    // enough delocalization, because there are not enough buckets in the main part of the curve
    // In this case, boost the coefficients at -1 and 1 to produce the expected delocalization
    static List<float> AdjustCoefficientsForDeloc(List<float> coefficients, float targetDelocalization, float binsize)
    {
        int maxIndex = coefficients.Count - 1;
        if (maxIndex > 0)
        {
            float delocSum = 0.0f;
            float nonZeroCoeffSum = 0.0f;
            for (int index = 1; index <= maxIndex; index += 1)
            {
                // the delocalization is the distance squared times the coefficient
                // and counted twice, once for negative, once for positive
                float distance = index * binsize;
                delocSum += 2 * distance * distance * coefficients[index];
                nonZeroCoeffSum += 2 * coefficients[index];
            }

            // the meaning of these sums:
            // nonZeroCoeffSum is the fraction between 0 and 1 of the
            // origin bucket redistributed to other buckets
            // delocSum is the amount of delocalization resulting from that distribution
            if (maxIndex > 1)
            {
                // to get the target delocalization, multiply each non-zeroBucket
                // by targetDelocalization / delocSum  and subtract the net change from
                // the origin bucket
                float boostFactor = (targetDelocalization / delocSum) - 1.0f;
                float transferredSum = 0.0f;
                // extra deloc should be less than coeff[0]
                for (int index = 1; index <= maxIndex; index += 1)
                {
                    float nthBoost = boostFactor * coefficients[index];
                    coefficients[index] += nthBoost;
                    transferredSum += nthBoost;
                }

                // 
                coefficients[0] -= 2 * transferredSum;

            }
            else if (maxIndex == 1)
            {
                // to achieve the extra deloc, calculate how much of coeff[0] we need to
                // move to coeff[1] value to compensate
                float extraDelocNeeded = targetDelocalization - delocSum;
                float amountToTransfer = extraDelocNeeded / (binsize * binsize);
                coefficients[0] -= amountToTransfer;
                coefficients[1] += amountToTransfer * 0.5f;
            }
            if (coefficients[0] < 0.0f)
            {
                // something has gone very wrong
                throw new Exception("AdjustCoefficientsForDeloc value out of bounds");
            }
        }
        return coefficients;
    }

    public DensityPlane CalculateTwoDDensity(List<VoxelID> voxelIds, int dimx, int dimy, float binsize, float delocalizationDistance)
    {
        DensityPlane densityPlane = new(binsize);
        for (int v = 0; v < voxelIds.Count; ++v)
        {
            VoxelID voxelId = voxelIds[v];
            var voxel = pcaVoxels[voxelId];
            var xVal = voxel.scoreVector[dimx];
            var yVal = voxel.scoreVector[dimy];
            densityPlane.AddPointAt(xVal, yVal);
        }
        if (delocalizationDistance > (binsize / 2))
        {
            List<float> coefficients = CoefficientsForExtraDelocalization(delocalizationDistance, binsize);

            // Now, apply the 'gaussian blur' using the coefficients in the 
            // normalizedCoefficients list.  Loop through each point in 
            // densityPlane and, for each point, fill the values
            // in a new densityPlane

            densityPlane = densityPlane.Convolve(coefficients);
         }

        return densityPlane;
    }

    internal static List<float> CoefficientsForExtraDelocalization(float delocalizationDistance, float binsize)
    {
        float delocRequested = delocalizationDistance * delocalizationDistance;
        float delocRemaining = delocRequested - (binsize * binsize * 0.25f);
        // gaussian smoothing parameter calculated from this:
        float gaussianSmoothParam = (float)MathF.Sqrt(2.0f * MathF.PI * delocRemaining);
        float binsizeOverGaussianSmoothParam = binsize / gaussianSmoothParam;
        int smoothingCutoffLimit = (int)MathF.Ceiling((1.0f / binsizeOverGaussianSmoothParam) * 1.5f);
        List<double> smoothCoefficients = new();
        for (int h = 0; h <= smoothingCutoffLimit; ++h)
        {
            // for gridpoint h, calculate the strength of the 1D gaussian at that 
            // distance. The gridpoint h is at a distance h * binsize from zero 
            // so, pass in h as x, and binsizeOverGaussianSmoothParam as inverseG
            smoothCoefficients.Add(GaussianOfXWithInverseG(h, binsizeOverGaussianSmoothParam));
        }

        // The sum of the coefficients should add up to one,
        // counting the non-zero values twice
        List<float> normalizedCoefficients = NormalizeCoefficients(smoothCoefficients); 
             
        // In the case of a small extra delocalization, a gaussian doesn't capture
        // enough delocalization, because there are not enough buckets in the main part of the curve
        // In this case, boost the coefficients at -1 and 1 to produce the expected delocalization
        List<float> adjustedCoefficients = AdjustCoefficientsForDeloc(normalizedCoefficients, delocRemaining, binsize);
        return adjustedCoefficients;
    }


    public DensityLine CalculateOneDDensity(List<VoxelID> voxelIds, int pcaDim, float binsize, float delocalizationDistance)
    {
        DensityLine densityLine = new(binsize);
        for (int v = 0; v < voxelIds.Count; ++v)
        {
            VoxelID voxelId = voxelIds[v];
            var voxel = pcaVoxels[voxelId];
            var xVal = voxel.scoreVector[pcaDim]; 
            densityLine.AddPointAt(xVal);
        }
        if (delocalizationDistance > (binsize / 2))
        {
            List<float> coefficients = CoefficientsForExtraDelocalization(delocalizationDistance, binsize);

            // Now, apply the 'gaussian blur' using the coefficients in the 
            // normalizedCoefficients list.  Loop through each point in 
            // densityPlane and, for each point, fill the values
            // in a new densityPlane

            densityLine = densityLine.Convolve(coefficients);
        }

        return densityLine;
    }
    // CalculateOneDDensities is not used, but here's what it does:
    // it creates a profile along each of the principal PCA dimensions 
    // of the density of voxels along that dimension 
    // That is, peaks in the density profile represent PCA score values with a high population of 
    // voxels. each Profile is a 'smoothed' histogram of populations 
    // The AP Suite already has code that produces a similar histogram, displayed
    // in one of the panes of the PCA Extension.
    // 
    // The number of points in the profile is determined by the binsize
    // If there are points between 0 and 10, there may be 23 entries, from -0.5 to 10.5
    // at spacings of 0.5
    //
    // It can be used like this:
    //
    // float binSeparation = 0.5f;
    // List<DensityProfile> oneDDensities = CalculateOneDDensities(voxelIndices, binSeparation);

    public List<DensityProfile> CalculateOneDDensities(List<VoxelID> voxelIds, float binsize)
    {
        List<DensityProfile> densities = new();
        for (int i = 0; i < this.scoreDims; ++i)
        {
            var nthDensityList = new DensityProfile(binsize);
            densities.Add(nthDensityList);
        }

        for (int v = 0; v < voxelIds.Count; ++v)
        {
            VoxelID voxelIndex = voxelIds[v];
            var voxel = pcaVoxels[voxelIndex];
            for (int i = 0; i < this.scoreDims; ++i)
            {
                densities[i].AddPointAt(voxel.scoreVector[i]);
            }
        }
        return densities;
    }

    public string GridInfo(string gridString)
    {
        string info = "";
        var gridId = new TwoDGridID(gridString);
        if (twoDPartitions.ContainsKey(gridId))
        {
            List<List<PixelID>> lists = twoDPartitions[gridId];
            DensityPlane plane = twoDGrids[gridId];
            int regionCount = lists.Count;
            info = "Regions Identified: " + regionCount;
            for (int r = 0; r < regionCount; ++r)
            {
                List<PixelID> rthList = lists[r];
                int peakCount = rthList.Count;
                // calculate center of peak
                float xSum = 0.0f;
                float ySum = 0.0f;
                foreach (PixelID pixelId in rthList)
                {
                    (float nthX, float nthY) = plane.XYCoordsFor(pixelId);
                    xSum += nthX;
                    ySum += nthY;
                }
                float xCenter = xSum / peakCount;
                float yCenter = ySum / peakCount;
                float area = peakCount * plane.binsize * plane.binsize;
                info += "\n  region " + r 
                      + "\n    pixels: " + peakCount.ToString()
                      + "\n    area: " + area.ToString()
                      + "\n    x center: " + xCenter.ToString()
                      + "\n    y center: " + yCenter.ToString();
            }
        }

        return info;
    }
    public string HistogramInfo(string histogramString)
    {
        string info = "";
        OneDGridID gridId = new(histogramString);
        if (oneDPartitions.ContainsKey(gridId))
        {
            List<List<BinID>> lists = oneDPartitions[gridId]; 
            DensityLine line = oneDGrids[gridId];
            int regionCount = lists.Count;
            info = "Regions Identified: " + regionCount;
            for (int r = 0; r < regionCount; ++r)
            {
                List<BinID> rthList = lists[r];
                BinID minBin = rthList.Min();
                BinID maxBin = rthList.Max();
                float lowBin = line.XValueFor(minBin);
                float highBin = line.XValueFor(maxBin);
                info += "\n  region " + r + "\n    low: " + lowBin.ToString() + "\n    high: " + highBin.ToString();
            }
        }

        return info;
    }
}
  