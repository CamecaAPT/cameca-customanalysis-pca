
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using System.Security.Cryptography.X509Certificates;

public struct BinID : IComparable<BinID>
{
    public int binId;

    public BinID(int binId)
    {
        this.binId = binId;
    }

    public int xCoord()
    {
        return (binId);
    }

    public int CompareTo(BinID other)
    {
        return other.binId > binId ? -1 : other.binId < binId ? 1 : 0;
    }

    public string DebugStr()
    {
        return binId.ToString();
    }
}


public struct RangeID
{
    BinID rangeBin;
    public RangeID(BinID binId)
    {
        rangeBin = binId;
    }

    public string DebugStr()
    {
        return rangeBin.DebugStr();
    }
}

// DensityLine represents a 1D density plot, and the bins exist on a 1D grid
// Bins are regularly spaced from the minimum to the maximum
// a binsize maps a floating point coordinate to a grid coordinate.
// if the binsize is 1, then the grid coordinate at p = 1, represents the floating point value 1.0 
// if the binsize is 0.5, then the grid coordinate at p = 1 represents the floating point value 0.5 
//
// The data is collected in a Dictionary<BinID, float>
// if a bin is unpopulated, there is no Dictionary entry for the corresponding ID
// smoothing is applied at population time: when a point is added,  it is automatically split 
// between bins to preserve a constant smoothing for all added points
// There is always a bin at zero
// Any int is a valid binID, but data is not added for bins at MaxInt and MinInt, 
// by enforcing "out of bounds" limits at (int.MaxValue - 2) * binSize;
// points outside of the bounds are counted but not binned
// 
// Points are added to the Line using a splat transfer function, in a way that the 
// delocalization for every point added to the profile is almost constant.  That is,
// if a point is added exatly between two bin points, it contributes .5 to each of the two bins.
// If a point is added exactly at a bin point, it contributes .75 to that bin and .125 to each of the two
// bins above and below it.

public class DensityLine
{
    float halfBinsize;
    public float binsize;
    float oneOverBinsize;
    int oobPoints;
    float maxval;
    Dictionary<BinID, float> data;
    
    public DensityLine(float binSize)
    {
        binsize = binSize;
        halfBinsize = binSize * 0.5f; 
        oobPoints = 0;
        oneOverBinsize = 1.0f / binSize;
        data = new Dictionary<BinID, float>();
        maxval = (int.MaxValue - 2) * binSize; // setting to (MaxBucket-2) ensures there will be a bucket above and below the target bucket for a splat
    }

    public delegate void ForEachBinCallback(BinID binId, float value);

    public void ForEachBin(ForEachBinCallback callback)
    {
        foreach (KeyValuePair<BinID, float> kvp in data)
        {
            callback(kvp.Key, kvp.Value);
        }
    }

    public DensityLine Convolve(List<float> normalizedCoefficients)
    {
        DensityLine newDL = new DensityLine(this.binsize);
        if (normalizedCoefficients.Count > 0)
        {
            int maxx = normalizedCoefficients.Count - 1;
            int minx = -maxx;
            int maxy = maxx;
            int miny = minx;
            void convolutionFunction(BinID binId, float value)
            {
                (int p, int q) = binId.xyCoords();
                for (int x = minx; x <= maxx; x+=1)
                {
                    int xCoefficientIndex = (int)Math.Abs(x);
                    float xCoeff = normalizedCoefficients[xCoefficientIndex];

                    BinID? nthBinID = BinID.BinIDFor(p + x);
                    if (nthBinID != null)
                    {  
                        newDP.AddValueAtBin(nthBinID.Value, value * xCoeff * yCoeff);
                    }
                }
            }
            ForEachBin(convolutionFunction);
        }
        return newDL;
    }

    public void AddValueAtBin(BinID binId, float value)
    {
        data[binId] = valueAtBin(binId) + value;
    }

    public List<BinID> GridPointIds()
    {
        return data.Keys.ToList();
    }

    // check for the 8 neighboring pixels and add them if they have a non-zero value
    public List<BinID> NeighboringNonZeroBins(BinID binId)
    {
        //int x;
        //int y;
        (int x) = binId.xCoord();
        List<BinID> neighbors = new List<BinID>();
        if (x > int.MinValue)
        {
            neighbors.Add(BinID(x - 1));
        }
        if (x < int.MaxValue)
        {
            neighbors.Add(BinID(x + 1));
        }
		return neighbors;
	}
	
    public float MaximumValue(List<BinID> candidates)
    {
        float maxScore = float.MinValue;

        foreach (BinID candidate in candidates)
        {
            float nthScore = valueAtBin(candidate);
            if (nthScore > maxScore)
            {
                maxScore = nthScore;
            }
        }
        return maxScore;
    }
    
    public BinID? FindMaximum(List<BinID> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }
        BinID bestCandidate = candidates[0];
        float currMax = valueAtBin(bestCandidate);
        foreach (BinID candidate in candidates)
        {
            float nthPopulation = valueAtBin(candidate);
            if (nthPopulation > currMax)
            {
                currMax = nthPopulation;
                bestCandidate = candidate;
            }
        }
        return bestCandidate;
    }
    
    public (BinID, BinID) MinMaxBinIDs()
    {
        if (data.Count == 0)
        {
            return (BinID(0), BinID(0));
        }
        
        // find the min and max bin IDs
        var binIds = data.Keys.ToList();
        binIds.Sort();
        return (binIds.First, binIds.Last);
    }
    
    public void WriteToStream(StreamWriter stream)
    {
        OneDGridCoord min;
        OneDGridCoord max;
        (min, max) = MinMaxGridCoords();

        int xSize = 1 + max.x - min.x;
		stream.WriteLine("x size= " + xSize);

		stream.Write("{ " );
		// now csv data for the grid from min to max
        for (int x = min.x; x <= max.x; ++x)
        {
            BinID xthBinId= BinID(x);


            float xthValue = 0;
            if (data.ContainsKey(xthBinId))
            {
                xthValue = data[xthBinId];
            }
            l = l + xthValue;
            if (x != max)
            {
                l = l + ",";
            }

        }
        l = l + "}";
        stream.Write(l);
        if (y != max.y)
        {
            stream.Write(",");
        }
		stream.Write("}");
    }
    
    public void ConsoleDump(string prefix)
    {
        int minx = 0;
        int maxx = 0;
        bool first = true;
        Debug.WriteLine("DensityLine Dump " + prefix);
        var binIds = data.Keys.ToList();
        binIds.Sort();
        foreach (BinID binId in binIds)
        {
            int x = binId.xCoord();
            if (!first)
            {
                minx = Math.Min(x, minx);
                maxx = Math.Max(x, maxx);
            }
            else
            {
                minx = x;
                maxx = x;
                first = false;
            }
            float binx = x * binsize;
            Debug.WriteLine("x = " + binx + ", population = " + data[binId]);
        }
    }
    
    public void writeToFile(string outputFilename)
    {   
        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        using (StreamWriter outputFile = new StreamWriter(outputFilename))
        {
            this.WriteToStream(outputFile);
        }
    }

    public float valueAtBin(BinID binId)
    {
        float binValue;
        if (data.TryGetValue(binId, out binValue))
        {
            return binValue;
        }
        return 0.0f;
    }

    public void AddPointAt(float x)
    {
        float Neg1Func(float x)
        {
            return (.5f * MathF.Pow(x - 0.5f, 2));
        }

        float ZeroFunc(float x)
        {
            return (.75f - MathF.Pow(x, 2));
        }

        float Pos1Func(float x)
        {
            return (.5f * MathF.Pow(x + 0.5f, 2));
        }

        void addValueAtCoord(int x, float val)
        {
            BinID binId = BinID(x);
            AddValueAtBin(binId.Value, val);
        }

        if ((Math.Abs(x) > maxval) || (Math.Abs(y) > maxval)) {
            oobPoints += 1;
            return;
        }

        int xBinIndex = MathF.Floor((x + halfBinsize) * oneOverBinsize);

        float xRem = (x - (xBinIndex * binsize)) * oneOverBinsize;
        float xm1 = (float)Neg1Func(xRem);
        float xp1 = (float)Pos1Func(xRem);    // these are the spline transfer functions
        float x0 = (float)ZeroFunc(xRem); 

        addValueAtCoord(xBinIndex - 1, xm1);
        addValueAtCoord(xBinIndex, x0);
        addValueAtCoord(xBinIndex + 1, xp1);
    }
}



