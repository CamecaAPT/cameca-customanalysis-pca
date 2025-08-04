using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using PcaExtensionMethods;

namespace Cameca.CustomAnalysis.Pca.VoxelLogic;

public class TwoDGridPartitionFinder
{
    readonly DensityPlane grid;
    List<PixelID> unplacedPixelIds; 
    // In the case of just one peak, we'll also identify pixels well away from the main peak, 
    // just as we do for one D partitioning
    List<PixelID> longTailPixelIds;
    // peakBorderPixelIDs will ultimately not be included in the peak pixels"
    // they will be used as a basis for defining the border zone between peaks
    readonly HashSet<PixelID> peakBorderPixelIds;
    // when a peak finds an increase, remember it here in foundIncreasePixelIds
    // when an increase is found, it means that there is likely another peak 
    // in the DensityPlane that is as yet unfound. The returnCode ReturnCode.foundIncrease
    // indicates that the pixel evaluation should be inturrupted and a new peak discovered
    List<PixelID> foundIncreasePixelIds;   
    readonly Dictionary<PeakID, TwoDPeak> peaks;
    PcaPhaseIdentificationProperties properties;
    readonly float borderExclusionRatio;

    // These are the return codes returned by IterateIdentifyingPeaks()
    // Depending on this result, the Logic in FindPartitions will either
    // seach for a new peak, or lower the search floor parameter provided to 
    // IterateIdentifyingPeaks()
    public enum ReturnCode {
        noStatus,
        foundIncrease,
        bestSuggestionBelowFloor,
        exhaustedSuggestions
    }

    public float PeakSummitAllowance()
    {
        return properties.peakSummitAllowance;
    }
    public TwoDGridPartitionFinder(DensityPlane twoDGrid, PcaPhaseIdentificationProperties props)
    {
        this.grid = twoDGrid;
        this.peaks = new Dictionary<PeakID, TwoDPeak> ();
        this.peakBorderPixelIds = new HashSet<PixelID>();
        this.foundIncreasePixelIds = new List<PixelID>();
        this.longTailPixelIds = new List<PixelID>();
        this.unplacedPixelIds = twoDGrid.GridPointIds();
        this.properties = props;
        this.borderExclusionRatio = 0.2f;
    }

    // for each iteration step,
    // 1) ask the different peaks if they have found an increase
    //    if yes -- make new peak, also, pixel that found increase
    //    should get transferred to rejected pixel list
    //    if no increse found, then ... otherwise
    // 2) ask the different peaks to suggest 
    //    their next target pixel(s)
    // 3) find highest valued pixel(s)
    // 4) reject pixels that collide
    //    -- notify each peak of rejection
    //    -- place rejected pixels in rejected bucket
    //    -- remove from unplacedPixelIds
    // 5) stop if highest valued pixel is below floor
    // 6) accept all highest valued pixels
    //    -- notify each peak of acceptance and
    //    -- remove from unplacedPixelIds

    public ReturnCode IterateIdentifyingPeaks(float noiseFloor)
    {
        bool keepGoing = true;
        ReturnCode returnCode = ReturnCode.noStatus;
        while (keepGoing)
        {
            CandidateRanker candidateRanker = new();

            //step 1
            foreach (PeakID peakKey in peaks.Keys.ToList())
            {
                TwoDPeak nthPeak = peaks[peakKey];
                if (nthPeak.HasFoundIncrease()) {
                    // need to clean out the 'HasFoundIncrease' flag from the peaks
                    // this will get done for all the peaks by the caller if
                    // we return the 'found increase' flag
                    return ReturnCode.foundIncrease; 
                }
            }


            // step 2
            candidateRanker.Clear();
            foreach (PeakID peakKey in peaks.Keys.ToList())
            {
                TwoDPeak nthPeak = peaks[peakKey];
                var nthSuggestions = nthPeak.NextSuggestions();
                foreach (PixelSuggestion nthSuggestion in nthSuggestions)
                {
                    candidateRanker.Consider(nthSuggestion);
                }
            }

            // step 3
            List<PixelSuggestion> bestCandidates = candidateRanker.bestCandidates();
            if (bestCandidates.Count == 0)
            {
                return ReturnCode.exhaustedSuggestions;
            }

            // step 4
            // count all the instances of each ID
            Dictionary<PixelID, int> pixelIdCounts = new();
            foreach (PixelSuggestion suggestion in bestCandidates)
            {
                if (pixelIdCounts.ContainsKey(suggestion.pixelId))
                {
                    pixelIdCounts[suggestion.pixelId] = pixelIdCounts[suggestion.pixelId] + 1;
                }
                else
                {
                    pixelIdCounts[suggestion.pixelId] = 1;
                }
            }
            // if there are any elements of the pixelIdCounts dictionary with value more than 1,
            // that's a collision

            List<PixelID> collisions = new();
            List<PixelID> pixelIdCountsKeys = pixelIdCounts.Keys.ToList();
            foreach (PixelID pixelId in pixelIdCountsKeys)
            {
                if (pixelIdCounts[pixelId] > 1)
                {
                    collisions.Add(pixelId);
                }
            }

            // foreach (PixelID collidingPixelId in collisions)
            if (collisions.Count > 0)
            {
                List<PixelSuggestion> tempCandidates = bestCandidates;
                bestCandidates = new List<PixelSuggestion>();
                // remove the collisions from bestCandidates
                // add the ids to peakBorderPixelIds
                // notify peaks of rejection

                foreach (PixelSuggestion suggestion in tempCandidates)
                {
                    if (collisions.Contains(suggestion.pixelId))
                    {
                        peaks[suggestion.peakId].SuggestionRejected(suggestion);
                    }
                    else
                    {
                        bestCandidates.Add(suggestion);
                    }
                }

                foreach (PixelID pixelId in collisions)
                {
                    peakBorderPixelIds.Add(pixelId);
                    unplacedPixelIds.Remove(pixelId);
                }
            }


            // step 6
            // for each accepted suggestion, notify the peak of Acceptance,
            // remove the id from unplaced IDs

            foreach (PixelSuggestion suggestion in bestCandidates)
            {
                // step 5 -- make sure the score is above the floor
                if (suggestion.score < noiseFloor)
                {
                    returnCode = ReturnCode.bestSuggestionBelowFloor;
                    keepGoing = false;
                }
                else
                {
                    peaks[suggestion.peakId].SuggestionAccepted(suggestion);
                    unplacedPixelIds.Remove(suggestion.pixelId);
                }
            }
        }
        return returnCode;
    }


    public void Clear()
    {
        peaks.Clear();
        peakBorderPixelIds.Clear();
        unplacedPixelIds = new List<PixelID>();
        longTailPixelIds = new List<PixelID>();
        unplacedPixelIds = grid.GridPointIds();
    }

    public List<List<PixelID>> GetPixelLists()
    {
        List<List<PixelID>> pixelLists = new();
        foreach (PeakID peakKey in peaks.Keys.ToList())
        {
            TwoDPeak nthPeak = peaks[peakKey];
            List<PixelID> peakPixelList = nthPeak.PixelList();
            pixelLists.Add(peakPixelList);
        }
        if (longTailPixelIds.Count > 0)
        {
            pixelLists.Add(longTailPixelIds);
        }
        return pixelLists;
    }

    // FindPartitions sets up the initial conditions for identifying peaks in the DensityPlane
    // and calls IterateIdentifyingPeaks() to do most of the work
    //
    // each time it calls IterateIdentifyingPeaks() it provides a floor for values to look for
    // The initial search floor is essentially a cutoff for how much of the peak to accept
    // Initially it is tied here to the 10% of the value of the highest peak (the noise floor fraction)
    //   -- this will be a good parameter to expose to users.
    // However, the search floor is also set when the algorithm knows that higher peak must exist 
    // above a certain value (because it found a pixel not connected to an existing peak with a higher value)
    // When this happens, the algorithm searches for another peak and its related pixels
    // As long as the pixel it found is not in fact part of new peak, the algorithm knows it should 
    // look for another peak
    public void FindPartitions(float noiseFloorFraction)
    {
        float noiseFloor = grid.MaximumValue(unplacedPixelIds) * noiseFloorFraction;
        PixelID? maybeMaximumPixelId = grid.FindMaximum(unplacedPixelIds);
        bool shouldContinueWithLowerSearchFloor = false;
        while (maybeMaximumPixelId.HasValue || shouldContinueWithLowerSearchFloor)
        {
            if (!shouldContinueWithLowerSearchFloor && maybeMaximumPixelId.HasValue)
            {
                PixelID maximumPixelId = maybeMaximumPixelId.Value;
                TwoDPeak nextPeak = new(this, grid, maximumPixelId);
                peaks[nextPeak.peakId] = nextPeak;
            }

            shouldContinueWithLowerSearchFloor = false;
            float higherPixelVal = 0.0f;
            if (foundIncreasePixelIds.Count > 0)
            {
                higherPixelVal = grid.ValueAtPixel(foundIncreasePixelIds[0]);
            }
            float searchFloor = Math.Max(higherPixelVal, noiseFloor);
            ReturnCode returnCode = IterateIdentifyingPeaks(searchFloor);

            switch (returnCode)
            {
                case ReturnCode.noStatus:
                    {
                        Debug.Write("returnCode noStatus -- must be error");
                        break;
                    }
                case ReturnCode.foundIncrease:
                    {
                            // need to remove the 'found increase' buckets from the peaks;
                            // also, collect these so that we can remember to look for new peaks
                        foreach (PeakID peakId in peaks.Keys.ToList())
                        {
                            List<PixelID> higherPixels = peaks[peakId].ClearFoundIncreases();
                            foreach (PixelID pixelId in higherPixels)
                            {
                                foundIncreasePixelIds.AddSorted(pixelId);
                            }
                        }
                        maybeMaximumPixelId = grid.FindMaximum(unplacedPixelIds);
                        break;
                    }
                case ReturnCode.bestSuggestionBelowFloor:
                    {
                        // its possible we have another peak to find
                        // first. filter any previous identified higherPixels to see if they are now part of a new peak:
                        foundIncreasePixelIds = FilterForAvailableIds(foundIncreasePixelIds);
                        maybeMaximumPixelId = grid.FindMaximum(unplacedPixelIds);
                        if (foundIncreasePixelIds.Count == 0) 
                        {
                            // if a new peak was identified, it has absorbed the pixels we found
                            // we should continue with a lower search floor
                            shouldContinueWithLowerSearchFloor = (noiseFloor < searchFloor);

                            // alternatively, it is possible there is a completely separate peak
                            // in which case, the previous peak did not find an increase, but we should continue with 
                            // identifying that new peak.
                            // in this case, see if maybeMaximumPixelId has a value above the searchFloor
                            // if it doesn't remove it from consideration
                            if (maybeMaximumPixelId.HasValue)
                            {
                                var nextPixelId = maybeMaximumPixelId.Value;
                                float nextPixelVal = grid.ValueAtPixel(nextPixelId);
                                if (nextPixelVal < noiseFloor)
                                {
                                    maybeMaximumPixelId = null;
                                }
                            }
                        }
                        break;
                    }
                case ReturnCode.exhaustedSuggestions:
                    {
                        maybeMaximumPixelId = grid.FindMaximum(unplacedPixelIds);
                        break;
                    }
            }
        }

        // now, the peaks dictionary is complete, but there is still work to do.
        // What exactly that work is depends on the number of peaks found
        //
        // If there are multiple peaks found, that extra work consists of cleaning up the borders
        // each peak has some pixels in its borderPixelIds list, and our algorithm has a collection 
        // of pixelIds in its "collisions" list
        // together, these pixels constitute the boundaries between the identified peaks.
        // Part of the algorithm should be to establish a border zone between the peaks of a certain width
        // because voxels near the borders could indeed be part of the tail in either region.
        // So, lets define a parameter W, between 0 and 1 but likely around 0.25, such that any pixel 
        // whose distance to a border/collision pixel is less than W times its distance to the peak max
        // should be considered as part of the border region, and therefore voxels that land in the 
        // those pixels should not get a designation from either peak.  Lets call that
        // W parameter the borderExclusionRatio

        // So, to do that calculation now.  THis is the case of multiple peaks, so all this
        // logic goes in an if {} case

        if (peaks.Count > 1)
        {

            // first, generate a list of all the collision/border pixel IDs
            // loop through all the pixels identified to be part of each peak, and test for this condition.
            // This seems like it should be an O(2) operation, because both the number of peak pixels and 
            // the number of border pixels grow as the grid size gets smaller, but the number of border pixels 
            // only grows as the root of number of pixels in the peak, so the complexity scaling is actually
            // not as bad as it might seem.  Still, this will be an issue for the smallest grid sizes.

            foreach (var peak in peaks.Values)
            {
                var borderIds = peak.BorderPixelIds();
                peakBorderPixelIds.UnionWith(borderIds);
            }

            // now peakBorderPixelIds contains all the borderIds
            // run the loop through all the peaks
            //
            // FURURE: at some point, we might consider
            // remembering which peaks were on the border, so that 
            // we have more information about how to assign voxels.
            // i.e. rather than just have voxels in these pixels get a ? 
            // designation, we do have some information about which regions they
            // might be a part of
            List<PixelID> excludedPixelIDs = new List<PixelID>();
            foreach (var peak in peaks.Values)
            {
                List<PixelID> peakExcludedPixelIds = peak.ExcludePixelsNear(peakBorderPixelIds, borderExclusionRatio);
                excludedPixelIDs.AddRange(peakExcludedPixelIds);
            }
        }
        else if (peaks.Count == 1)
        {
            // if there is only one peak identified in the main routine,
            // there is no need to do border cleanup
            // however, a grid with only one peak is not very useful in 
            // partitioning the voxels in a sample  
            // In this case (like we do in the One D case), we can create 
            // a second degignation corresponding to the long tail of voxels not in 
            // or close to the main peak
            // FUTURE: in the future, we may look more carefully at the distribution
            // of these voxels and try to find cluster-ish groups

            // for now, lets use the most simple criterion we can find:  
            // pixels further from the main peak than 1/2 the distance to the peak max


            var keys = peaks.Keys.ToList();
            PeakID mainPeakID = keys[0];
            TwoDPeak mainPeak = peaks[mainPeakID];
            PixelID mainPeakPixelId = mainPeak.peakMaxPixelId;
            PixelCoords mainPeakPixelCoords = mainPeakPixelId.PixelCoords();
            List<PixelID> rimPixelIds = mainPeak.AllNextCandidatesPixelIDs();

            // rimPixelIds are the pixels not in the peak but which are adjacent
            // to a pixel in the peak
            // Our method for determining inclusion in the border area is:
            // Identifying the closest rim pixel to 
            // The distance of a Pixel Pc (candidatePixel) to the rim
            // first involves finding the closest rim pixel Pr
            // the distance between those two pixels will be
            // be called Dcprp (distance of candidate pixel to rim pixel)
            // the actual distance to the rim is slightly larger,
            //   Dcpr = 0.5 + Dcprp
            // Dcpr is (distance of candidate pixel to rim)
            // 0.5 is added as the distance between the rim pixel and 
            // pixels actually in the peak.  The fact that this is a
            // "Manhattan distance" means that the algorithm is slightly non-ideal 
            // but we are working in a fictitious PCA space anyway.
            // Distance of the candidate pixel to the Main peak is 
            //   Dcpmp
            // So, we'll define a border zone as any pixel such that 
            //    Dcpr * 2 <= Dcpmp
            // And alternatively, pixels further away from the rim such that 
            //    Dcpr > Dcpmp
            // A SIMPLE EXAMPLE:
            // there is a peak of just five pixels, {0,0} and its 4 neighbors
            //    {1,0}, {0,1}, {-1, 0}, {0, -1}
            // candidate pixels at {1,1} {2,0} {0,2} are rim pixels, all of
            // which have a "distance to rim pixel" of 0 (distance to themselves)
            // and therefore a "distance to rim of 0.5
            // all of them are further than 1.0 to the main peak, so all of them are
            // in the border zone
            // A pixel at {1,2} is a distance 1 away from a rim pixel, and therefore 
            // a distance 1.5 from the rim
            // That pixel is closer to the main peak than 3.0 (its distance is sqrt(5))
            // so that pixel is outside the border zone
            // A pixel at {0,3} is also a distance 1 away from a rim pixel ({0,2})
            // it's distance is exactly 3 away from the main peak, so it is inside the 
            // border zone (the criterion is Dcpr * 2 <= Dcpmp, not Dcpr * 2 < Dcpmp)
            // In this simple case, the border zone does look oddly shaped, but 
            // such is the nature of a square grid and use of Manahttan distance..  Larger peaks will have 
            // more reasonable border zone shapes.

            foreach (var candidatePixel in unplacedPixelIds)
            {
                // Use DSquared rather than D to avoid a squareroot calculation
                var distSquaredToMainPeak = candidatePixel.DSquaredTo(mainPeakPixelCoords);
                // if we can't find a rim pixel closer than the main peak
                // we'll use mainPeak pixelID
                float closestDSqu = distSquaredToMainPeak;
                PixelID closestRimPixelID = mainPeakPixelId;
                foreach (var rimPixelId in rimPixelIds)
                {
                    var distSquaredToRimPixel = candidatePixel.DSquaredToPixel(rimPixelId);
                    if (distSquaredToRimPixel < closestDSqu)
                    {
                        closestDSqu = distSquaredToRimPixel;
                        closestRimPixelID = rimPixelId;
                    }
                }
                // do one square root so we can add 0.5
                double Dcpr = 0.5 + Math.Sqrt(closestDSqu);

                // border zone test is
                //     Dcpr * 2 <= Dcpm
                // but less calculation required to test 
                //     DcprSquared * 4 <= DcpmpSquared
                if (Dcpr * Dcpr * 4 <= distSquaredToMainPeak)
                {
                    // candidate pixel is in border zone
                }
                else
                {
                    // candidate pixel is in long tail group
                    longTailPixelIds.Add(candidatePixel);
                }
            }

            // now remove longTailPixelIds from unplacedPixelIds  
            foreach (var longTailPixelId in longTailPixelIds)
            {
                unplacedPixelIds.Remove(longTailPixelId);
            }

            // now the grid is partitioned into
            // the main peak (mainPeak.pixelIDs)
            // the borderZone (unplacedPixelIDs)
            // long tail (longTailPixelIDs)

        }
    }

    public List<PixelID> FilterForAvailableIds(List<PixelID> pixelIds)
    {
        // return a list containing all the items in the
        // input list which are in the unplacedPixelIds list
        return pixelIds.Where(pixelId => unplacedPixelIds.Contains(pixelId)).ToList();
    }
}   





