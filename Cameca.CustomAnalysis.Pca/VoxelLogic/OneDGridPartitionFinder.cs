using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using PcaExtensionMethods;
using Cameca.CustomAnalysis.Pca;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.ObjectModel;


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
        List<List<BinID>> binLists = new List<List<BinID>>();
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
    // any other dimension will not produce peaks that don't exiast independently in 
    // the other doimensions 1D profile. 
    // 
    //  However, it is possible that the PCA analysis along this dimension can be used
    // to partition voxels inside the main peak and those outside it.  In somr analyses, 
    // this manifests as a wide distribution of voxels along the axis, but recognizably outside
    // the main peak.
    //
    // So, the basic strategy here will be -- identify some properties of the main peak 
    // its position, its max value, width at half max, width at quarter max, etc.
    // then try to estimate where to draw a line between peak and non-peak components
    //
    // we'll model the peak as a bi-gaussian, and use the top 3/4 of the peak as a guide to find where 
    // to cut off the peak region, and also how wide a border region should be defined.
    public List<List<BinID>> FindPartitions()
    {
        List<List<BinID>> partitions = new List<List<BinID>> ();
        BinID? maybeMaximumBinId = line.FindMaximum(unplacedBinIds);

        if (maybeMaximumBinId != null)
        {
            float firstMaximum = line.ValueAtBin(maybeMaximumBinId.Value);
            float noiseLevel = firstMaximum * 0.05f;
            while ((maybeMaximumBinId != null) && (line.ValueAtBin(maybeMaximumBinId.Value) > noiseLevel))
            {
                BinID binId = maybeMaximumBinId.Value;
                OneDPeak peak = new OneDPeak(this, line, binId);
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
        if (peaks.Count == 1)
        {
            List<RangeID> keys = peaks.Keys.ToList();
            OneDPeak peak = peaks[keys[0]];
            List<BinID> borderBinIds = peak.BorderBins();
            if (borderBinIds.Count == 2)
            {
                List<BinID> binList = new List<BinID>();
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