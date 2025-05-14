
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
using System.Collections.ObjectModel;

namespace Cameca.CustomAnalysis.Pca.VoxelLogic;

public readonly struct BinID : IComparable<BinID>
{
    public readonly int binId;

    public BinID(int binId)
    {
        this.binId = binId;
    }

    public int XCoord()
    {
        return (binId);
    }
    public BinID NextLowerBin()
    {
        return new BinID(binId - 1);
    }
    public BinID NextHigherBin()
    {
        return new BinID(binId + 1);
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


public readonly struct RangeID
{
    readonly BinID rangeBin;
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
    readonly float halfBinsize;
    public float binsize;
    readonly float oneOverBinsize;
    int oobPoints;
    readonly float maxval;
    readonly Dictionary<BinID, float> data;
    
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

    public BinID LocalMaxNear(BinID binId)
    {
        BinID lowerBin = binId.NextLowerBin();
        BinID higherBin = binId.NextHigherBin();
        float lowerBinValue = ValueAtBin(lowerBin);
        float higherBinValue = ValueAtBin(higherBin);
        float binVal = ValueAtBin(binId);
        if ((binVal < higherBinValue) || (binVal < lowerBinValue))
        {
            if (lowerBinValue > higherBinValue)
            {
                while (lowerBinValue > binVal)
                {
                    binId = lowerBin;
                    lowerBin = binId.NextLowerBin();
                    binVal = lowerBinValue;
                    lowerBinValue = ValueAtBin(lowerBin);
                }
            }
            else
            {
                while (higherBinValue > binVal)
                {
                    binId = higherBin;
                    higherBin = binId.NextHigherBin();
                    binVal = higherBinValue;
                    higherBinValue = ValueAtBin(higherBin);
                }
            }
        }
        return binId;
    }

    public void ForEachBin(ForEachBinCallback callback)
    {
        foreach (KeyValuePair<BinID, float> kvp in data)
        {
            callback(kvp.Key, kvp.Value);
        }
    }

    public DensityLine Convolve(List<float> normalizedCoefficients)
    {
        DensityLine newDL = new(this.binsize);
        if (normalizedCoefficients.Count > 0)
        {
            int maxx = normalizedCoefficients.Count - 1;
            int minx = -maxx;
            int maxy = maxx;
            int miny = minx;
            void convolutionFunction(BinID binId, float value)
            {
                int p = binId.XCoord();
                for (int x = minx; x <= maxx; x+=1)
                {
                    int xCoefficientIndex = (int)Math.Abs(x);
                    float xCoeff = normalizedCoefficients[xCoefficientIndex];
                    newDL.AddValueAtBin(new BinID(p + x), value * xCoeff);
                }
            }
            ForEachBin(convolutionFunction);
        }
        return newDL;
    }

    public void AddValueAtBin(BinID binId, float value)
    {
        data[binId] = ValueAtBin(binId) + value;
    }

    public List<BinID> GridPointIds()
    {
        return data.Keys.ToList();
    }

    public int? BinIndexFor(float x)
    {
        if ((x < maxval) && (x > -maxval))
        {
            int binIndex = (int)MathF.Floor((x + halfBinsize) * oneOverBinsize);
            return binIndex;
        }
        else
        {
            return null;
        }
    }

    public BinID? BinIDFor(float x)
    {
        int? binx = BinIndexFor(x);
        return binx == null ? null : new BinID(binx.Value);
    }
    public float XValueFor(BinID binId)
    {
        return binsize * binId.XCoord();
    }

    // check for the 8 neighboring pixels and add them if they have a non-zero value
    public static List<BinID> NeighboringNonZeroBins(BinID binId)
    {
        //int x;
        //int y;
        int x = binId.XCoord();
        List<BinID> neighbors = new();
        if (x > int.MinValue)
        {
            neighbors.Add(new BinID(x - 1));
        }
        if (x < int.MaxValue)
        {
            neighbors.Add(new BinID(x + 1));
        }
		return neighbors;
	}
	
    public float MaximumValue(List<BinID> candidates)
    {
        float maxScore = float.MinValue;

        foreach (BinID candidate in candidates)
        {
            float nthScore = ValueAtBin(candidate);
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
        float currMax = ValueAtBin(bestCandidate);
        foreach (BinID candidate in candidates)
        {
            float nthPopulation = ValueAtBin(candidate);
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
            BinID zeroBin = new(0);
            return (zeroBin, zeroBin);
        }
        
        // find the min and max bin IDs
        var binIds = data.Keys.ToList();
        binIds.Sort();
        return (binIds.First(), binIds.Last());
    }
    
    public void WriteToStream(StreamWriter stream)
    {
        BinID min;
        BinID max;
        (min, max) = MinMaxBinIDs();

        string line = string.Empty;
        int xSize = 1 + max.XCoord() - min.XCoord();
		stream.WriteLine("x size= " + xSize);

		stream.Write("{ " );
        int maxx = max.XCoord();
		// now csv data for the grid from min to max
        for (int x = min.XCoord(); x <= maxx; ++x)
        {
            BinID xthBinId= new(x);

            float xthValue = 0;
            if (data.ContainsKey(xthBinId))
            {
                xthValue = data[xthBinId];
            }
            line += xthValue;
            if (x != maxx)
            {
                line += ",";
            }

        }
        line += "}";
        stream.Write(line);
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
            int x = binId.XCoord();
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
    
    public void WriteToFile(string outputFilename)
    {   
        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        using (StreamWriter outputFile = new(outputFilename))
        {
            this.WriteToStream(outputFile);
        }
    }

    public float ValueAtBin(BinID binId)
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
            AddValueAtBin(new BinID(x), val);
        }

        if (Math.Abs(x) > maxval) {
            oobPoints += 1;
            return;
        }

        int xBinIndex = (int)MathF.Floor((x + halfBinsize) * oneOverBinsize);

        float xRem = (x - (xBinIndex * binsize)) * oneOverBinsize;
        float xm1 = (float)Neg1Func(xRem);
        float xp1 = (float)Pos1Func(xRem);    // these are the spline transfer functions
        float x0 = (float)ZeroFunc(xRem); 

        addValueAtCoord(xBinIndex - 1, xm1);
        addValueAtCoord(xBinIndex, x0);
        addValueAtCoord(xBinIndex + 1, xp1);
    }
}



