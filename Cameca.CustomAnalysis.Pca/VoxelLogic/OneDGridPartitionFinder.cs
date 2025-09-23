using System.Collections.Generic;
using System.Linq;
using System;

namespace Cameca.CustomAnalysis.Pca.VoxelLogic;

public class OneDGridPartitionFinder
{
    readonly DensityLine line;
    List<BinID> unplacedBinIds;
    readonly List<BinID> rejectedBinIds;
    readonly List<BinID> foundIncreaseBinIds;
    List<BinID> longTailBinList;
    readonly Dictionary<RangeID, OneDPeak> peaks;
    PcaPhaseIdentificationProperties properties;

    public OneDGridPartitionFinder(DensityLine densityLine, PcaPhaseIdentificationProperties props)
    {
        this.line = densityLine;
        this.peaks = new Dictionary<RangeID, OneDPeak> ();
        this.rejectedBinIds = new List<BinID>();
        this.foundIncreaseBinIds = new List<BinID>();
        this.longTailBinList = new List<BinID>();
        this.unplacedBinIds = densityLine.GridPointIds();
        this.properties = props;
    }
 

    public void Clear()
    {
        peaks.Clear();
        rejectedBinIds.Clear();
        unplacedBinIds.Clear();
        unplacedBinIds = line.GridPointIds();
    }

    public List<List<BinID>> GetBinLists()
    {
        List<List<BinID>> binLists = new();
        foreach (RangeID peakKey in peaks.Keys.ToList())
        {
            OneDPeak nthPeak = peaks[peakKey];
            List<BinID> peakBinList = nthPeak.BinList();
            binLists.Add(peakBinList);
        }
        if (longTailBinList.Count > 0)
        {
            binLists.Add(longTailBinList);
        }
        return binLists;
    }

    // FindPartitions attempts to separate the oneDGrid into regions -- 
    // generally, this is intended to be used in the case of a single main peak,
    // or one extremely dominant peak, such that this dimension is not very useful
    // in a 2d partitioning scheme -- that is, using this dimension in a 2D grid with 
    // any other dimension will not produce peaks that don't exist independently in 
    // the other doimensions 1D profile. 
    // 
    //  However, it is possible that the PCA analysis along this dimension can be used
    // to partition voxels inside the main peak and those outside it.  In some analyses, 
    // this manifests as a wide distribution of voxels along the axis, but recognizably outside
    // the main peak.
    public List<List<BinID>> FindPartitions()
    {
        List<List<BinID>> partitions = new();
        BinID? maybeMaximumBinId = line.FindMaximum(unplacedBinIds);

        if (maybeMaximumBinId != null)
        {
            float firstMaximum = line.ValueAtBin(maybeMaximumBinId.Value);
            float noiseLevel = firstMaximum * 0.05f;
            while ((maybeMaximumBinId != null) && (line.ValueAtBin(maybeMaximumBinId.Value) > noiseLevel))
            {
                BinID binId = maybeMaximumBinId.Value;
                OneDPeak peak = new(this, line, binId);
                peak.IdentifyBins(noiseLevel, unplacedBinIds);
                List<BinID> binList = peak.BinList();
                partitions.Add(binList);
                binList.ForEach(bin => { unplacedBinIds.Remove(bin); });
                // also remove borderBins
                peak.BorderBins().ForEach(bin => { unplacedBinIds.Remove(bin); });

                peaks[peak.rangeId] = peak;
                maybeMaximumBinId = line.FindMaximum(unplacedBinIds);
            }
        }

        // Now, if only one peak was identified, create an artificial second peak with all the points well away from the first peak
        // We should restrict the second artificial peak to one side of the main peak
        // Although it is possible that significant number of voxels could live on the
        // negative side of the main peak, it is usually the case that the long tail
        // exists on the positive side

        // borderBinIds[0] is the bin on the lower side
        // borderBinIds[1] is the bin on the upper side
        // longTailBinList will only consist of bins on the upper side
        if (peaks.Count == 1)
        {
            List<RangeID> keys = peaks.Keys.ToList();
            OneDPeak peak = peaks[keys[0]];
            List<BinID> borderBinIds = peak.BorderBins();
            if (borderBinIds.Count == 2)
            {
                List<BinID> binList = new();
                float border1 = line.XValueFor(borderBinIds[0]); 
                float border2 = line.XValueFor(borderBinIds[1]);
                float upperBar = MathF.Max(border1 + 0.5f, border2 + 0.5f);
                unplacedBinIds.ForEach(bin =>
                {
                    float xVal = line.XValueFor(bin);
                    if (xVal > upperBar)
                    {
                        binList.Add(bin);
                    }
                });
                longTailBinList = binList;
                partitions.Add(binList);
            }
        }
        
        return partitions;
    }

    public List<BinID> FilterForAvailableIds(List<BinID> binIds)
    {
        // return a list containing all the items in the
        // input list which are in the unplacedBinIds list
        return binIds.Where(binId => unplacedBinIds.Contains(binId)).ToList();
    }
    public float PeakSummitAllowance()
    {
        return properties.peakSummitAllowance;
    }
    public float NoiseFloorFraction()
    {
        return properties.noiseFloorFraction;
    }
}   