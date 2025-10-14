using System.Collections;
using System.Collections.Generic;
using System;
using PcaExtensionMethods;
using System.Linq;

namespace Cameca.CustomAnalysis.Pca.VoxelLogic;

// PixelSuggestion represents a pixel that could be added to a TwoDPeak as part of the PartitionFinder
// peak partitioning algorithm
public struct PixelSuggestion : IComparable<PixelSuggestion>
{
    public float score;
    public PixelID pixelId;
    public PeakID peakId;

    public PixelSuggestion(PeakID peak, PixelID pixel, float score)
    {
        this.score = score;
        this.pixelId = pixel;
        this.peakId = peak;
    }

    public readonly int CompareTo(PixelSuggestion other)
    {
        return other.score < score ? -1 : other.score > score ? 1 : 0;
    }

    public readonly string DebugStr()
    {
        return "PixelID: " + pixelId.DebugStr() + ", score: " + score + ", peak: " + peakId.DebugStr();

    }
}


// TwoDPeak represents a maximum in a DensityPlane grid and its surrounding pixels, as part of
// the Partitioning algorithm implemented in TwoDGridPartitionFinder
// Initially it is created with a single pixel location, and it grows to surrounding pixels
// governed by the TwoDGridPartitionFinder.
//
// It maintains an ordered list of "pixels to add", and during the algorithm, repeated provides
// to PartitionFinder the next 'best candidate' for adding to itself.  PartitionFinder evaluates all the 
// candidates provided by all the peaks it knows about, and selects them.  When a candidate is selected
// the SuggestionAccepted method is called, and new potential candidates are added to its list of "pixels to add"
//
// As an example, if the densities look like this:
//
//   50     70    50     40    30
//   80    101   100     97    70
//   70     99    98     80    60
//   30     40    50     70    50   
//
// The peak would grow like this:
//
//  Cycle       values of ids in the Peak list   Values of Pixels in the candidates list 
//  0           {}                               {101}
//  1           {101}                            {100, 99, 80, 70}
//  2           {101, 100}                       {99, 98, 97, 80, 70, 50}
//  3           {101, 100, 99}                   {98, 97, 80, 70, 70, 50, 40}
//  4           {101, 100, 99, 98}               {97, 80, 80, 70, 70, 50, 50, 40}
//  5           {101, 100, 99, 98, 97}           {80, 80, 70, 70, 70, 50, 50, 40, 40}
//  6           {101, 100, 99, 98, 97, 80, 80}   {70, 70, 70, 70, 60, 50, 50, 50, 40, 40}
//
// After step 5, both of the '80' pixels would be returned as part of the best candidates.
//
// A future logical addition:  right now, the algorithm won't handle the case very well where what should really 
// be a single peak is actually two maxima very close together. Some logic should be added so that when a 
// "increase" is found above a certain fraction of the peak maximum, it is not considered as evidence of a second peak.
//
// As an example, if the densities look like this:
//
//   50     70    50     40    30
//   80    101   100     97    70
//   70    100    98     99    80
//   30     40    50     70    50    
//
// The algorthim will find two peaks but it should probably only find one
// The logic could be that if a pixel at higher than some threshhold of
// the peak maximum, say 75%, finds an increase, it shouldn't warn of an 'increase found'
// result, but just accumulate that pixel as well
// in the example above, the peak would start at 101, then grow to the two 100 pixels.
// the next candidate offered/accepted would be the 98 pixel.  At that point, the 99
// pixel would be considered for adding to the candudates list, but as it is an increase from 
// 98, algorithm would flag it as the case where a second peak must exist.
public class TwoDPeak
{
    public PeakID peakId;
    public PixelID peakMaxPixelId; // this is also the id for this object in container's dictionary
    readonly List<PixelID> borderPixelIds;
    readonly HashSet<PixelID> inPeakPixelIds;
    readonly List<PixelID> excludedPixelIds;
    readonly List<PixelSuggestion> nextCandidates;
    readonly List<PixelSuggestion> topCandidates; // this list is recycled as the return vehicle for getting next suggestions
    readonly DensityPlane referenceGrid;
    List<PixelID> foundIncreaseIds; // list of pixelIds at edge which increase relative to neighbor
    readonly TwoDGridPartitionFinder partitionFinder; // will supply available neighbors
    readonly float peakValue;
    readonly float summitAllowance;  // the value above which to ignore 'second peak'

    public TwoDPeak(TwoDGridPartitionFinder partitionFinder, DensityPlane grid, PixelID peakMaxPixelId)
    {
        this.partitionFinder = partitionFinder;
        this.peakMaxPixelId = peakMaxPixelId;
        this.peakId = new PeakID(peakMaxPixelId);
        this.inPeakPixelIds = new HashSet<PixelID>();
        this.borderPixelIds = new List<PixelID>();
        this.excludedPixelIds = new List<PixelID>();
        this.nextCandidates = new List<PixelSuggestion>();
        this.topCandidates = new List<PixelSuggestion>();
        this.referenceGrid = grid;
        this.foundIncreaseIds = new List<PixelID>();
        this.summitAllowance = partitionFinder.PeakSummitAllowance(); // hard code this value

        this.peakValue = grid.ValueAtPixel(peakMaxPixelId);
        PixelSuggestion firstSuggestion = new(peakId, peakMaxPixelId, this.peakValue);

        nextCandidates.Add(firstSuggestion);
    }

    // this gets called at the final step of PartitionFinder -- 
    // the algorithm says we should have a zone around the border between peaks
    // such that pixel closer than PMD * W to a borderPixel should not be considered
    // part of the peak, where W is the borderExclusionRatio, and 
    // PMD is the distance of the pixel to its peak maximum pixel.
    public List<PixelID> ExcludePixelsNear(HashSet<PixelID> allBorderPixelIds, float borderExclusionRatio)
    {

        // pixel coords are integers of the pixel grid
        PixelCoords peakMaxPixelCoords = peakMaxPixelId.PixelCoords();
        float borderExclusionRatioSquared = borderExclusionRatio * borderExclusionRatio;
        List<PixelID> excludedPixels = new List<PixelID>();

        foreach (var pixelID in inPeakPixelIds)
        {
            // first calculate the distance of each peak pixel to the peak max  
            // and to avoid an expensive squareroot calculation, use the
            // distance squared in our comparisons
            var dSquaredToPeakMax = pixelID.DSquaredTo(peakMaxPixelCoords);
            var pixelCoords = pixelID.PixelCoords();
            foreach (var borderPixel in allBorderPixelIds)
            {
                var dSquaredToBorder = borderPixel.DSquaredTo(pixelCoords);

                // here, we want to test if
                //      dToBorder < dToPeakMax * borderExclusionRatio
                // that's the same test as 
                //      dSquaredToBorder < dSquaredToPeakMax * borderExclusionRatioSquared
                // assuming none of the values are negative (LOL)

                if (dSquaredToBorder < dSquaredToPeakMax * borderExclusionRatioSquared)
                {
                    // In this case, the pixel is closer to the border than the threshold -- 
                    // it should be part of the border zone, and not part of the peak
                    excludedPixels.Add(pixelID);
                    break; // exit the borderPixel foreach loop
                }
            }
        }

        // all the pixels in the excludedPixels HashMap should be taken out of the 
        // inPeakPixelIds
        inPeakPixelIds.ExceptWith(excludedPixels);
        return excludedPixels;
    }

    public List<PixelID> AllNextCandidatesPixelIDs()
    {
        List<PixelID> pixelIds = new List<PixelID>();
        foreach (var pixelSuggestion in nextCandidates)
        {
            pixelIds.Add(pixelSuggestion.pixelId);
        }
        return pixelIds;
    }
    public List<PixelID> PixelList()
    {
        return inPeakPixelIds.ToList();
    }

    public List<PixelID> BorderPixelIds()
    {
        return borderPixelIds;
    }

    public bool HasFoundIncrease()
    {
        return foundIncreaseIds.Count > 0;
    }

    // if suggestion accepted, remove from list and 
    // add adjacent pixels to list
    public void RemoveFromCandidates(PixelSuggestion suggestion)
    {
        nextCandidates.Remove(suggestion);
    }
    // if suggestion accepted, remove from list and 
    // add adjacent pixels to list
    public void SuggestionAccepted(PixelSuggestion suggestion)
    {
        RemoveFromCandidates(suggestion);
        bool isBorder = false;
        
        List<PixelID> newPossibilities = referenceGrid.PixelIdsNeighboring(suggestion.pixelId);
        List<PixelID> availableIds = partitionFinder.FilterForAvailableIds(newPossibilities);
        foreach(PixelID availableId in availableIds)
        {
            // this might already be in our list of candidates -- if it is, skip

            if (!IsCandidate(availableId))
            {
                float neighborScore = referenceGrid.ValueAtPixel(availableId);

                // for now, hard code 
                if ((neighborScore > suggestion.score) && (suggestion.score < this.peakValue * this.summitAllowance))
                {
                    // neighbor has higher score -- shouldn't add to current peak
                    // the currently 'accepted suggestion' is actually a pixel between peaks.
                    isBorder = true;
                    foundIncreaseIds.Add(availableId);
                }
                else
                {
                    PixelSuggestion newSuggestion = new(peakId, availableId, neighborScore);
                    this.InsertCandidate(newSuggestion);
                }
            }
        }
        if (isBorder)
        {
            borderPixelIds.Add(suggestion.pixelId);
        }
        else
        {
            inPeakPixelIds.Add(suggestion.pixelId);
        }
    }

    public bool IsCandidate(PixelID pixelId)
    {
        foreach ( PixelSuggestion candidate in  nextCandidates)
        {
            if (candidate.pixelId.pixelId == pixelId.pixelId)
            {
                return true;
            }
        }
        return false;
    }

    public List<PixelID> ClearFoundIncreases()
    {
        List<PixelID> returnList =  foundIncreaseIds;
        foundIncreaseIds = new List<PixelID>();
        return returnList;
    }


    // this peak maintains a list of the candidate neighbor pixels 
    // which could be added to the peak during the accumulation
    // the 
    public void InsertCandidate(PixelSuggestion candidate)
    {
        nextCandidates.AddSorted(candidate);
    }
    // if suggestion rejected, remove from list
    public void SuggestionRejected(PixelSuggestion suggestion)
    {
        RemoveFromCandidates(suggestion);
    }

    public List<PixelSuggestion> NextSuggestions()
    {
        topCandidates.Clear();
        if (nextCandidates.Count > 0)
        {
            var topCandidate = nextCandidates[0];
            topCandidates.Add(topCandidate);
            float bestScore = topCandidate.score;
            bool keepGoing = true;
            int nextIndex = 1;
            while (keepGoing)          
            {
                if (nextIndex < nextCandidates.Count)
                {
                    topCandidate = nextCandidates[nextIndex];
                    if (topCandidate.score < bestScore)
                    {
                        keepGoing = false;
                    }
                    else
                    {
                        topCandidates.Add(topCandidate);
                        nextIndex += 1;
                    }  
                }
                else { keepGoing = false; }
                
            }
        }
        return topCandidates;
    }
}





