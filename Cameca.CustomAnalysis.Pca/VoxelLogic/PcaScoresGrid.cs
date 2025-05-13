
using PcaExtensionMethods;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using System.IO;
using System.Windows.Controls;
using System.Collections;
using System.Windows.Input;
using System.Text;
using System.Windows.Media.Animation;
using System.Reflection.PortableExecutable;
using Cameca.CustomAnalysis.Pca;
using System.Runtime.Intrinsics.Arm;
using System.Net.Http;
using System.Data.SqlTypes;
using System.Reflection;
using System.Windows;
using System.Reflection.Metadata.Ecma335;


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
    Dictionary<VoxelID, PcaVoxel> pcaVoxels; // the key is the 'id' for the voxel, from which you can
                                             // calculate the x,y,z position of the voxel on the grid if
                                             // the grid dimensions are known

    Dictionary<TwoDGridID, List<List<PixelID>>> twoDPartitions; // key is grid ID
          // value is lists of associations of voxelID with different peaks

    Dictionary<TwoDGridID, DensityPlane> twoDGrids;  // key is grid ID

    Dictionary<OneDGridID, List<List<BinID>>> oneDPartitions; // key is grid ID
          // value is lists of associations of binID with different partitions

    Dictionary<OneDGridID, DensityLine> oneDGrids;  // key is grid ID

    int scoreDims;
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
    internal int ASCIIValueForChar(char s)
    {
        return (int)s;
    }

    internal char CharValueForASCII(int a)
    {
        return (char)a;
    }
    // returns a list of 27 indices, unless the voxel is near the edge
    // note: also includes self
    List<VoxelID> NeighborIDsFor(ThreeDGridCoord gridCoord)
    {
        List<VoxelID> neighborIndices = new List<VoxelID>();

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
                    VoxelID voxelId = new VoxelID(x + (y * gridDims.x) + (z * gridDims.xy));
                    neighborIndices.Add(voxelId);
                }
            }
        }
        return neighborIndices;
    }

    public PcaScoresGrid(IScoresProvider scoresProvider, int nIndices, int scoreDimensions, ThreeDGridDimensions gridDimensions)
    {
        scoreDims = scoreDimensions;

        pcaVoxels = pcaVoxelsInit(scoresProvider, nIndices);

        gridDims = gridDimensions;
        twoDPartitions = new Dictionary<TwoDGridID, List<List<PixelID>>>();
        twoDGrids = new Dictionary<TwoDGridID, DensityPlane>();
        oneDPartitions = new Dictionary<OneDGridID, List<List<BinID>>>();
        oneDGrids = new Dictionary<OneDGridID, DensityLine>();
    }

    static Dictionary<VoxelID, PcaVoxel> pcaVoxelsInit(IScoresProvider scoresProvider, int nIndices)
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
    Dictionary<PixelID, List<VoxelID>> aggregateVoxelsIntoListsPerPixel(List<VoxelID> voxelIds, DensityPlane grid, Dictionary<VoxelID, PcaVoxel> pcaVoxels, int firstDim, int secondDim)
    {
        // For each voxel, assign the voxel to the List corresponding with its pixel
        Dictionary<PixelID, List<VoxelID>> voxelLists = new Dictionary<PixelID, List<VoxelID>>();
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

    Dictionary<BinID, List<VoxelID>> aggregateVoxelsIntoListsPerBin(List<VoxelID> voxelIds, DensityLine line, Dictionary<VoxelID, PcaVoxel> pcaVoxels, int pcaDimension)
    {
        // For each voxel, assign the voxel to the List corresponding with its bin
        Dictionary<BinID, List<VoxelID>> voxelLists = new Dictionary<BinID, List<VoxelID>>();
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
    internal Dictionary<PcaPhaseName, HashSet<VoxelID>> VoxelBuckets(Dictionary<VoxelID, PcaPhaseName> pcaCodes)
    {
        Dictionary<PcaPhaseName, HashSet<VoxelID>> buckets = new Dictionary<PcaPhaseName, HashSet<VoxelID>>();
        foreach (KeyValuePair<VoxelID, PcaPhaseName> kvp in pcaCodes)
        {
            if (buckets.ContainsKey(kvp.Value))
            {
                buckets[kvp.Value].Add(kvp.Key);
            }
            else
            {
                HashSet<VoxelID> newSet = new HashSet<VoxelID>();
                newSet.Add(kvp.Key);
                buckets[kvp.Value] = newSet;
            }
        }
        return buckets;
    }
    internal Dictionary<PcaPhaseName, int> PcaCodeCounts(Dictionary<PcaPhaseName, HashSet<VoxelID>> voxelBuckets)
    {
        Dictionary<PcaPhaseName, int> pcaCodeCounts = new Dictionary<PcaPhaseName, int>();
        foreach (KeyValuePair<PcaPhaseName, HashSet<VoxelID>> kvp in voxelBuckets)
        {
            pcaCodeCounts[kvp.Key] = kvp.Value.Count;
        }
        return pcaCodeCounts;
    }
    internal void DumpPcaCodeCounts(Dictionary<string, int> pcaCodeCounts)
    {
        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string outputFilename = System.IO.Path.Combine(docPath, "PcaCodeStats.txt");

        using (StreamWriter outputFile = new StreamWriter(outputFilename))
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

    internal void DumpPartitions(List<List<PixelID>> partitions, string gridId)
    {
        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string outputFilename = System.IO.Path.Combine(docPath, "PcaPeakPartitions" + gridId + ".txt");
        // int peakIndex = 1;
        using (StreamWriter outputFile = new StreamWriter(outputFilename))
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
                    (int x, int y) = pixelId.xyCoords();
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

    internal List<VoxelID> FindMatchingSets(VoxelID neighborID, Dictionary<VoxelID, HashSet<VoxelID>> voxelSets)
    {
        List<VoxelID> matchingSets = new List<VoxelID>();
        foreach (KeyValuePair<VoxelID, HashSet<VoxelID>> kvp in voxelSets)
        {
            if (kvp.Value.Contains(neighborID))
            {
                matchingSets.Add(kvp.Key);
            }
        }
        return matchingSets;
    }

    // filterForMatchableCode identifies which of the pcaCodes in nonZeroPcaCodes are
    // 'one-away' matches for pcaCode
    // The reason we use a . to separate the letter from the number in PCA codes is to 
    // make it easy to identify a zero -- i.e. the algorithm here also
    // works if there are 10 peaks, because "A.10" doesn't contain ".0"
    internal HashSet<PcaPhaseName> filterForMatchableCode(List<PcaPhaseName> nonZeroPcaCodes, PcaPhaseName pcaCode)
    {
        // if pcaCode contains zero 0s or more than one 0, return empty List
        HashSet<PcaPhaseName> matches = new HashSet<PcaPhaseName>();

        // tokens here means 'substrings' -- parts of the PCA code that should be matched
        string[] tokens = pcaCode.Split(".0");
        int numTokens = tokens.Count();
        if (numTokens > 1)
        {
            foreach (PcaPhaseName candidate in nonZeroPcaCodes)
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

    void addVoxelsToHashSetDictionary(Dictionary<PcaPhaseName, HashSet<VoxelID>> additions, Dictionary<PcaPhaseName, HashSet<VoxelID>> sets)
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

    void addVoxelsToPcaPhaseNameListSet(Dictionary<PcaPhaseNameList, HashSet<VoxelID>> additions, Dictionary<PcaPhaseNameList, HashSet<VoxelID>> sets)
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
    int subtractVoxelsFromHashSetDictionary(Dictionary<PcaPhaseName, HashSet<VoxelID>> subtractions, Dictionary<PcaPhaseName, HashSet<VoxelID>> sets)
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
                    throw new Exception("subtractVoxels expected all voxels to exist in previous bucket");
                }
                unassignedCount += actuallyRemoved;
                sets[kvp.Key] = hashSet;
            }
            else
            {
                throw new Exception("subtractVoxels expected existing bucket of voxels");
            }
        }
        return unassignedCount;
    }

    public OneDGridsResults CalculateOneDGrids(PcaPhaseIdentificationProperties properties)
    {
        OneDGridsResults gridsResults = new OneDGridsResults();
        oneDGrids.Clear();
        oneDPartitions.Clear();

        List<VoxelID> voxelIds = pcaVoxels.Keys.ToList();

        float binSeparation = properties.oneDProjectionBinSize; 
        float delocalization = properties.oneDProjectionDelocalization;
        int numDimsToInclude = this.scoreDims;
 
        // now, make a oneD grid for each dimensions
        for (int i = 0; i < (numDimsToInclude - 1); ++i)
        {
 
            OneDGridID gridId = new OneDGridID(i);

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
        TwoDGridsResults gridsResults = new TwoDGridsResults();
        twoDGrids.Clear();
        twoDPartitions.Clear();

        List<VoxelID> voxelIds = pcaVoxels.Keys.ToList();

        float binSeparation = properties.gridProjectionBinSize;
        float delocalization = properties.gridProjectionDelocalization;
        int numDimsToInclude = this.scoreDims;
 
        int AAsciiValue = ASCIIValueForChar('A');
        // now, make twoD grids using all pairs of dimensions
        for (int i = 0; i < (numDimsToInclude - 1); ++i)
        {
            for (int j = i + 1; j < numDimsToInclude; ++j)
            {
                TwoDGridID gridId = new TwoDGridID(i, j);

                // this makes the 2D grid  --  step A) above
                var twoDGrid = CalculateTwoDDensity(voxelIds, i, j, binSeparation, delocalization);
                twoDGrids[gridId] = twoDGrid;

                // this identifies the peaks --  step B) above
                List<List<PixelID>> partitionedIndices = IdentifyTwoDPartitions(twoDGrid, i, j, properties);
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
    public PhaseIdResults GetPhasesStrategyF(PcaPhaseIdentificationProperties properties, HashSet<string> twoDridsToExclude, HashSet<string> oneDGridsToInclude)
    {
        List<VoxelID> voxelIds = pcaVoxels.Keys.ToList();
        int numDimsToInclude = Math.Min(properties.numDimsForPCAPhaseId, this.scoreDims);

        // make a dictionary for the pcaCodes and fill with empty Strings
        Dictionary<VoxelID, PcaPhaseName> pcaCodes = new Dictionary<VoxelID, PcaPhaseName>();
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
            if (!twoDridsToExclude.Contains(gridID.ToString()))
            {
                DensityPlane twoDGrid = twoDGrids[gridID];
                List<List<PixelID>> partitionedIndices = kvp.Value;
                (int firstDim, int secondDim) = gridID.AsIndexPair();

                Dictionary<PixelID, List<VoxelID>> voxelLists = aggregateVoxelsIntoListsPerPixel(voxelIds, twoDGrid, pcaVoxels, firstDim, secondDim);
                int peakIndex = 1;

                foreach (List<PixelID> pixelIdList in partitionedIndices)
                {
                    string pcaCode = gridID.ToString() + "." + peakIndex.ToString();
                    // the pixelIdList contains a list of pixelIds identified as being part of the Nth partition
                    foreach (PixelID pixelID in pixelIdList)
                    {
                        // Lookup for all the voxels bucketed under this pixelId
                        List<VoxelID> voxelIdsForThisPixel = voxelLists[pixelID];
                        foreach (VoxelID voxelId in voxelIdsForThisPixel)
                        {
                            pcaCodes[voxelId] = pcaCodes[voxelId].AppendCode(pcaCode);
                        }
                        // remove that entry from voxelLists
                        voxelLists.Remove(pixelID);
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
            OneDGridID gridId = new OneDGridID(s);
            DensityLine oneDGrid = oneDGrids[gridId];
            List<List<BinID>> partitionedIndices = oneDPartitions[gridId];
            int pcaDimension = gridId.PCAIndex();
            Dictionary<BinID, List<VoxelID>> voxelLists = aggregateVoxelsIntoListsPerBin(voxelIds, oneDGrid, pcaVoxels, pcaDimension);
            int peakIndex = 1; 
            foreach (List<BinID> binIdList in partitionedIndices)
            {
                string pcaCode = gridId.ToString() + "." + peakIndex.ToString();
                // the pixelIdList contains a list of pixelIds identified as being part of the Nth partition
                foreach (BinID binId in binIdList)
                {
                    // Lookup for all the voxels bucketed under this binId
                    List<VoxelID> voxelIdsForThisBin = voxelLists[binId];
                    foreach (VoxelID voxelId in voxelIdsForThisBin)
                    {
                        pcaCodes[voxelId] = pcaCodes[voxelId].AppendCode(pcaCode);
                    }
                    // remove that entry from voxelLists
                    voxelLists.Remove(binId);
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
        PcaStream pcaStream = new PcaStream("GetPhasesStrategyF");
        PhaseIdResults phaseIdResults = new PhaseIdResults(voxelIds);

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
        Dictionary<PcaPhaseName, HashSet<VoxelID>> pcaCodeVoxelSets = new Dictionary<PcaPhaseName, HashSet<VoxelID>>();
        Dictionary<PcaPhaseNameList, HashSet<VoxelID>> interfaceVoxelSets = new Dictionary<PcaPhaseNameList, HashSet<VoxelID>>();

        // pcaCodes is a Dictionary<VoxelID, string>
        foreach (PcaPhaseName pcaCode in nonZeroPcaCodes)
        {
            // just copy over the voxel List to be the base list for  that phase
            pcaCodeVoxelSets[pcaCode] = unassignedVoxelBuckets[pcaCode];
            unassignedVoxelBuckets.Remove(pcaCode);
        }

        // now pcaCodeVoxelSets is filled with data for each pcaCode,
        // These are our 'core' regions
        // now lets go through the voxels with PcaCodes that contain a single 0
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

        int voxelAssignmentCount = 1;
        while (voxelAssignmentCount > 0)
        {
            Dictionary<PcaPhaseName, HashSet<VoxelID>> unassignedSubtractions = new Dictionary<PcaPhaseName, HashSet<VoxelID>>();
            Dictionary<PcaPhaseName, HashSet<VoxelID>> voxelSetAdditions = new Dictionary<PcaPhaseName, HashSet<VoxelID>>();
            Dictionary<PcaPhaseNameList, HashSet<VoxelID>> interfaceVoxelSetAdditions = new Dictionary<PcaPhaseNameList, HashSet<VoxelID>>();

            // this loop initializes the lists of voxels to add for each PCA code
            int numNonZeroPcaCodes = nonZeroPcaCodes.Count();
            for (int nOuter = 0; nOuter < numNonZeroPcaCodes; ++nOuter)
            {
                PcaPhaseName nonZeroPcaCode = nonZeroPcaCodes[nOuter];
                voxelSetAdditions[nonZeroPcaCode] = new HashSet<VoxelID>();
            }

            List<PcaPhaseName> unassignedPhaseNames = unassignedVoxelBuckets.Keys.ToList();
            foreach (PcaPhaseName phaseName in unassignedPhaseNames)
            {
                HashSet<PcaPhaseName> matchableCodes = filterForMatchableCode(nonZeroPcaCodes, phaseName);
                // matchableCodes are the nonZeroPcaCodes that are "one away" from the pcaCode under consideration

                HashSet<VoxelID> potentialAdditions = unassignedVoxelBuckets[phaseName];
                HashSet<VoxelID> subtractions = new HashSet<VoxelID>();
                List<PcaPhaseName> matches = new List<PcaPhaseName>();
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
                    if (matches.Count() > 0)
                    {
                        if (matches.Count() == 1)
                        {
                            voxelSetAdditions[matches[0]].Add(voxelId);
                            subtractions.Add(voxelId);
                        }
                        else
                        {
                            PcaPhaseNameList interfaceId = new PcaPhaseNameList(matches);
                            if (interfaceVoxelSetAdditions.ContainsKey(interfaceId))
                            {
                                interfaceVoxelSetAdditions[interfaceId].Add(voxelId);
                            }
                            else
                            {
                                HashSet<VoxelID> newSet = new HashSet<VoxelID>();
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

            pcaStream.WriteTimestamp("finished voxel partitioning" );
            // pcaStream.DumpVoxelSetStats("start pcaCodeVoxelSets", pcaCodeVoxelSets); 
            // pcaStream.DumpVoxelSetStats("start pcaCodeVoxelSets", pcaCodeVoxelSets);
            // pcaStream.DumpVoxelSetStats("startUnassigned", unassignedVoxelBuckets);
            // pcaStream.DumpVoxelSetStats("voxelSetAdditions", voxelSetAdditions);
            // pcaStream.DumpVoxelSetStats("interfaceVoxelSetAdditions", interfaceVoxelSetAdditions);

            addVoxelsToPcaPhaseNameListSet(interfaceVoxelSetAdditions, interfaceVoxelSets);
            addVoxelsToHashSetDictionary(voxelSetAdditions, pcaCodeVoxelSets);
            voxelAssignmentCount = subtractVoxelsFromHashSetDictionary(unassignedSubtractions, unassignedVoxelBuckets);

            // pcaStream.DumpVoxelSetStats("assignments sources", unassignedSubtractions);
            //  pcaStream.DumpVoxelSetStats("new pcaCodeVoxelSets", pcaCodeVoxelSets);
            //  pcaStream.DumpVoxelSetStats("stillUnassigned", unassignedVoxelBuckets);
            pcaStream.WriteTimestamp("assigned " + voxelAssignmentCount + " voxels");
        }

        // now, pcaCodeVoxelSets is ready to be used for define a per-voxel component mapping
        // lets order the pcaCodeVoxelSets by population
        List<PcaPhaseName> pcaCodesByPopulation = pcaCodeVoxelSets.Keys.ToList();
        PopulationSorter<PcaPhaseName, VoxelID> comparator = new PopulationSorter<PcaPhaseName, VoxelID>(pcaCodeVoxelSets);
        pcaCodesByPopulation.Sort(comparator);

        // now pcaCodesByPopulation is sorted?
        // make a pcaCode to phaseIndex map:
        // 
        Dictionary<PcaPhaseName, int> phaseIndexMap = new Dictionary<PcaPhaseName, int>();
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

        pcaStream.Close();
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
    public List<List<BinID>> IdentifyOneDPartitions(DensityLine oneDGrid, int dimX, PcaPhaseIdentificationProperties props)
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
        OneDGridPartitionFinder partitionFinder = new OneDGridPartitionFinder(oneDGrid, props);

        partitionFinder.FindPartitions();
        var binLists = partitionFinder.GetBinLists();
        partitionFinder.Clear();
        return binLists;
    }


    // IdentifyTwoDPartitions use the density map from the twoDGrid to separate 
    // voxels that belong to different peaks in the DensityPlane
    // first, identify the peaks and their associated pixels
    // then, for each voxel, see if it lands in on of the partitioned pixels.
    // If it does, add it to the appropriate list
    // return the list of lists
    // indices not identified are not returned in any list
    public List<List<PixelID>> IdentifyTwoDPartitions(DensityPlane twoDGrid, int dimX, int dimy, PcaPhaseIdentificationProperties props)
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
        TwoDGridPartitionFinder partitionFinder = new TwoDGridPartitionFinder(twoDGrid, props);

        partitionFinder.FindPartitions(props.noiseFloorFraction);
        var pixelLists = partitionFinder.GetPixelLists();
        partitionFinder.Clear();
        return pixelLists;
    }
    private double gaussianOfXWithInverseG(float x, float inverseg)
    {
        double dx = (double)x;
        double dig = (double)inverseg;
        return (dig * Math.Exp(-Math.PI * x * x * dig * dig));
    }


    // The coefficients for positive values (i.e. all but the first one at index zero)
    // are also used for the negative values, and the area under the curve should be equal to one.
    // so, add up all the values and adjust them all so that the total is one.
    private List<float> normalizeCoefficients(List<double> coeffs)
    {
        int limit = coeffs.Count - 1;
        List<float> normalized = new List<float>();
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
    List<float> adjustCoefficientsForDeloc(List<float> coefficients, float targetDelocalization, float binsize)
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
                throw new Exception("adjustCoefficientsForDeloc value out of bounds");
            }
        }
        return coefficients;
    }

    public DensityPlane CalculateTwoDDensity(List<VoxelID> voxelIds, int dimx, int dimy, float binsize, float delocalizationDistance)
    {
        DensityPlane densityPlane = new DensityPlane(binsize);
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

    internal List<float> CoefficientsForExtraDelocalization(float delocalizationDistance, float binsize)
    {
        float delocRequested = delocalizationDistance * delocalizationDistance;
        float delocRemaining = delocRequested - (binsize * binsize * 0.25f);
        // gaussian smoothing parameter calculated from this:
        float gaussianSmoothParam = (float)MathF.Sqrt(2.0f * MathF.PI * delocRemaining);
        float binsizeOverGaussianSmoothParam = binsize / gaussianSmoothParam;
        int smoothingCutoffLimit = (int)MathF.Ceiling((1.0f / binsizeOverGaussianSmoothParam) * 1.5f);
        List<double> smoothCoefficients = new List<double>();
        for (int h = 0; h <= smoothingCutoffLimit; ++h)
        {
            // for gridpoint h, calculate the strength of the 1D gaussian at that 
            // distance. The gridpoint h is at a distance h * binsize from zero 
            // so, pass in h as x, and binsizeOverGaussianSmoothParam as inverseG
            smoothCoefficients.Add(gaussianOfXWithInverseG(h, binsizeOverGaussianSmoothParam));
        }

        // The sum of the coefficients should add up to one,
        // counting the non-zero values twice
        List<float> normalizedCoefficients = normalizeCoefficients(smoothCoefficients); 
             
        // In the case of a small extra delocalization, a gaussian doesn't capture
        // enough delocalization, because there are not enough buckets in the main part of the curve
        // In this case, boost the coefficients at -1 and 1 to produce the expected delocalization
        List<float> adjustedCoefficients = adjustCoefficientsForDeloc(normalizedCoefficients, delocRemaining, binsize);
        return adjustedCoefficients;
    }


    public DensityLine CalculateOneDDensity(List<VoxelID> voxelIds, int pcaDim, float binsize, float delocalizationDistance)
    {
        DensityLine densityLine = new DensityLine(binsize);
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
        List<DensityProfile> densities = new List<DensityProfile>();
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
}




