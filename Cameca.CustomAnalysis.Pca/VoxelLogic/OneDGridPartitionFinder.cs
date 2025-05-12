using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using PcaExtensionMethods;
using Cameca.CustomAnalysis.Pca;


public class OneDGridPartitionFinder
{
    DensityPlane grid;
    List<BinID> unplacedBinIds;
    List<BinID> rejectedBinIds;
    Dictionary<RangeID, OneDPeak> peaks;
    PcaPhaseIdentificationProperties properties;

    public OneDGridPartitionFinder(DensityPlane oneDGrid, PcaPhaseIdentificationProperties props)
    {
        this.grid = oneDGrid;
        this.peaks = new Dictionary<RangeID, OneDPeak> ();
        this.rejectedBinIds = new List<BinID>();
        this.foundIncreaseBinIds = new List<BinID>();
        this.unplacedBinIds = oneDGrid.GridPointIds();
        this.properties = props;
    }
 

    public void Clear()
    {
        peaks.Clear();
        rejectedBinIds.Clear();
        unplacedBinIds.Clear();
        unplacedBinIds = grid.GridPointIds();
    }

    public List<List<BinID>> GetBinLists()
    {
        List<List<BinID>> binLists = new List<List<BinID>>();
        foreach (RangeID peakKey in peaks.Keys.ToList())
        {
            OneDPeak nthPeak = peaks[peakKey];
            List<BinID> peakPixelList = nthPeak.PixelList();
            pixelLists.Add(peakPixelList);
        }
        return pixelLists;
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
    public FindPartitions()
    {


    }

    public List<BinID> filterForAvailableIds(List<BinID> binIds)
    {
        // return a list containing all the items in the
        // input list which are in the unplacedBinIds list
        return binIds.Where(binId => unplacedBinIds.Contains(binId)).ToList();
    }
}   





