using System.Collections.Generic;

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

    public OneDPeak(OneDGridPartitionFinder partitionFinder, DensityLine line, BinID peakMaxBinId)
    {
        this.partitionFinder = partitionFinder;
        this.peakMaxBinId = line.LocalMaxNear(peakMaxBinId);
        this.rangeId = new RangeID(peakMaxBinId);
        this.inPeakBinIds = new List<BinID>();
        this.referenceLine = line;
        this.borderBinIds = new List<BinID>();
        this.summitAllowance = partitionFinder.PeakSummitAllowance(); // hard code this value
        this.noiseFloorFraction = partitionFinder.NoiseFloorFraction(); // hard code this value

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

    // Strategy -- just go from the peakMaxBinId to the left and right until 
    // increase is found or noise level reached

    public void IdentifyBins(float noiseLevel, List<BinID> availableBins)
    {
        inPeakBinIds.Add(peakMaxBinId);
        float peakValue = referenceLine.ValueAtBin(this.peakMaxBinId);
        float summitThreshhold = peakValue * summitAllowance;

        // first find bins on the lower side of the peak
        BinID nextBin = peakMaxBinId.NextLowerBin();
        float nextValue = referenceLine.ValueAtBin(nextBin);
        float currentValue = peakValue;
        while (availableBins.Contains(nextBin) && (nextValue > noiseLevel) && ((nextValue > summitThreshhold) || (nextValue < currentValue)))
        {
            currentValue = nextValue;
            inPeakBinIds.Add(nextBin);
            nextBin = nextBin.NextLowerBin();
            nextValue = referenceLine.ValueAtBin(nextBin);
        }
        borderBinIds.Add(nextBin);

        // now, find bins on the upper side
        nextBin = peakMaxBinId.NextHigherBin();
        nextValue = referenceLine.ValueAtBin(nextBin);
        currentValue = peakValue;
        while (availableBins.Contains(nextBin) && (nextValue > noiseLevel) && ((nextValue > summitThreshhold) || (nextValue < currentValue)))
        {
            currentValue = nextValue;
            inPeakBinIds.Add(nextBin);
            nextBin = nextBin.NextHigherBin();
            nextValue = referenceLine.ValueAtBin(nextBin);
        }
        borderBinIds.Add(nextBin);
    }
}





