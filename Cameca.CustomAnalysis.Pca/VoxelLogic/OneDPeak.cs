using Cameca.CustomAnalysis.Utilities;
using System.Collections.Generic;
using System.IO;

namespace Cameca.CustomAnalysis.Pca.VoxelLogic;

// OneDPeak represents a maximum in a DensityLine grid and its surrounding bins, as part of
// the Partitioning algorithm implemented in OneDGridPartitionFinder
// Initially it is created with a single bin location, and it grows to surrounding bins
// governed by the OneDGridPartitionFinder.
//
// It maintains an ordered list of "bins to add", and during the algorithm, repeated provides
// to PartitionFinder the next 'best candidate' for adding to itself.  PartitionFinder evaluates all the 
// candidates provided by all the peaks it knows about, and selects them.  When a candidate is selected
// the SuggestionAccepted method is called, and new potential candidates are added to its list of "pixels to add"
//
public class OneDPeak
{
    public RangeID rangeId;
    readonly BinID peakMaxBinId; // this is also the id for this object in container's dictionary
    readonly List<BinID> borderBinIds;
    readonly List<BinID> inPeakBinIds;
    readonly DensityLine referenceLine;
    readonly OneDGridPartitionFinder partitionFinder; // will supply available neighbors
    readonly float peakValue;
    readonly float summitAllowance;  // the value above which to ignore 'second peak'
    readonly float noiseFloorFraction;  // the value below which to stop looking for peak bins
    readonly float borderExclusionRatio;  // the value below which to stop looking for peak bins

    public OneDPeak(OneDGridPartitionFinder partitionFinder, DensityLine line, BinID peakMaxBinId)
    {
        this.partitionFinder = partitionFinder;
        this.peakMaxBinId = line.LocalMaxNear(peakMaxBinId);
        this.rangeId = new RangeID(peakMaxBinId);
        this.inPeakBinIds = new List<BinID>();
        this.referenceLine = line;
        this.borderBinIds = new List<BinID>();
        this.summitAllowance = partitionFinder.PeakSummitAllowance();  
        this.noiseFloorFraction = partitionFinder.NoiseFloorFraction();  
        this.borderExclusionRatio = partitionFinder.BorderExclusionRatio();  

        this.peakValue = line.ValueAtBin(peakMaxBinId);
    }

    public List<BinID> BinList()
    {
        return inPeakBinIds;
    }
    public List<BinID> BorderBins()
    {
        return borderBinIds;
    }

    internal void PartitionBinsForBorder(List<BinID> candidates, BinID borderBin)
    {
        float peakToBorderDistance = (float)this.peakMaxBinId.DistanceTo(borderBin);
        float borderProximityThreshold = peakToBorderDistance * borderExclusionRatio;
        foreach (BinID bin in candidates)
        {
            float binToBorderDistance = (float)bin.DistanceTo(borderBin);
            if (binToBorderDistance > borderProximityThreshold)
            {
                inPeakBinIds.Add(bin);
            }
            else
            {
                borderBinIds.Add(bin);
            }
        }
    }

    internal void AddToInPeakBinIds(List<BinID> candidates, List<BinID> availableBins, BinID borderBin, float currentValue, float noiseLevel)
    {
        float borderValue = referenceLine.ValueAtBin(borderBin);
        // now add all binsInDescentRegion to inPeakBinIds if we found a noise floor
        // or if the bin is not in the border exclusion zone if we found an increase
        if (availableBins.Contains(borderBin) && (borderValue > noiseLevel) && (borderValue >= currentValue))
        {
            // this is the case where the next bins looks like an increase
            // apply the boderExclusionZone test
            PartitionBinsForBorder(candidates, borderBin);
        }
        else
        {
            // no borderExclusionZone test needed --
            // add all the bins in binsInDescentRegion to inPeakBinIds
            foreach (BinID bin in candidates)
            {
                inPeakBinIds.Add(bin);
            }
        }
    }
    
    // Strategy -- just go from the peakMaxBinId to the left and right until 
    // increase is found or noise level reached
    // if an increase is found, exclude bins near the border bin if 
    // the distance to the border is less than borderExclusionRatio times
    // distance to the peak
    public void IdentifyBins(float noiseLevel, float borderExclusionRatio, List<BinID> availableBins)
    {

        inPeakBinIds.Add(peakMaxBinId);
        float peakValue = referenceLine.ValueAtBin(this.peakMaxBinId);
        float summitThreshhold = peakValue * summitAllowance;

        // first find bins on the lower side of the peak
        BinID nextBin = peakMaxBinId.NextLowerBin();
        float nextValue = referenceLine.ValueAtBin(nextBin);
        float currentValue = peakValue;
        List<BinID> binsInDescentRegion = new List<BinID>();
        while (availableBins.Contains(nextBin) && (nextValue > noiseLevel) && ((nextValue > summitThreshhold) || (nextValue < currentValue)))
        {
            currentValue = nextValue;
            binsInDescentRegion.Add(nextBin);
            nextBin = nextBin.NextLowerBin();
            nextValue = referenceLine.ValueAtBin(nextBin);
        }
        borderBinIds.Add(nextBin);
        AddToInPeakBinIds(binsInDescentRegion, availableBins, nextBin, currentValue, noiseLevel);
 

        // now, find bins on the upper side
        nextBin = peakMaxBinId.NextHigherBin();
        nextValue = referenceLine.ValueAtBin(nextBin);
        binsInDescentRegion.Clear();
        currentValue = peakValue;
        while (availableBins.Contains(nextBin) && (nextValue > noiseLevel) && ((nextValue > summitThreshhold) || (nextValue < currentValue)))
        {
            currentValue = nextValue;
            binsInDescentRegion.Add(nextBin);
            nextBin = nextBin.NextHigherBin();
            nextValue = referenceLine.ValueAtBin(nextBin);
        }
        borderBinIds.Add(nextBin);
        AddToInPeakBinIds(binsInDescentRegion, availableBins, nextBin, currentValue, noiseLevel);
    }
}





