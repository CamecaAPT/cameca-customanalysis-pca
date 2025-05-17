
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Cameca.CustomAnalysis.Pca.VoxelLogic;

public struct PixelID : IComparable<PixelID>
{
    public readonly int pixelId;
    
    // Here are some hardcoded constants for maintaining the grid data
    // each grid row has potentially 2^10 pixelIDs per row (i.e. 1024 -- not all actually used)
    // advancing the y index by one advances the pixel ID by 1024
    // pixe3l ID 0 refers to the pixel at 0,0
    // other constants are defined here for ease of calculation later
    public const int GridStrideExp = 10;
    public const int GridStride = 1 << GridStrideExp;
    public const int HalfGridStride = 1 << (GridStrideExp - 1); // pixel ids advance 1024 from one row to the next
    public const int Maxgrid = HalfGridStride - 2; // buckets from -510 to 510 are possible

    public PixelID(int pixelId)
    {
        this.pixelId = pixelId;
    }

    public static PixelID? PixelIDFor(int x, int y)
    {
        if ((x > Maxgrid) || (x < -Maxgrid) || (y > Maxgrid) || (y < -Maxgrid))
        {
            return null;
        }
        int newId = y * GridStride + x;
        return new PixelID(newId);
    }

    public readonly (int, int) XYCoords()
    {
        int y = (HalfGridStride + pixelId) >> GridStrideExp;
        int x = pixelId - (y * GridStride);
        return (x, y);
    }

    public readonly int CompareTo(PixelID other)
    {
        return other.pixelId > pixelId ? -1 : other.pixelId < pixelId ? 1 : 0;
    }

    public readonly string DebugStr()
    {
        (int x, int y) = this.XYCoords();
        return pixelId.ToString() + ":{" + x + "," + y + "}";
    }
}


public readonly struct PeakID
{
    readonly PixelID peakMax;
    public PeakID(PixelID pixelId)
    {
        peakMax = pixelId;
    }

    public string DebugStr()
    {
        return peakMax.DebugStr();
    }
}

// DensityPlane represents a 2D density plot, and the bins exist on a 2D grid
// Bins are regularly spaced from the minimum to the maximum
// a binsize maps a floating point coordinate to a grid coordinate.
// if the binsize is 1, then the grid coordinate at p-1, q=1 represents the floating point coordinate 1.0, 1.0
// if the binsize is 0.5, then the grid coordinate at p-1, q=1 represents the floating point coordinate 0.5, 0.5
// Bins have an integer id derived from their p and q coordinates :  1024q + p 
// So, for a binsize of 1.0, the xy coordinate 3.0,4.0 would correspond to 
// the bin at p = 3, q = 4 and the ID would be 4099
// if the binsize is 0.5, a point at x=3, y=4 would be the gridpoint p=6, q=8, and the ID would be 8198
//
// The data is collected in a Dictionary<int, float>
// if a bin is unpopulated, there is no Dictionary entry for the corresponding ID
// smoothing is applied at population time: when a point is added,  it is automatically split 
// between bins to preserve a constant smoothing for all added points
// There is always a bin at zero
// bins at negative values have negative indices
// "out of bounds" limits at -510 and 510
//  points outside of the bounds are counted but not binned
// 
// Points are added to the Plane using a splat transfer function, in a way that the 
// delocalization for every point added to the profile is almost constant.  That is,
// if a point is added at a corner of the grid, equidistant from four different grid points, 
// it contributes .25 to each of the four bins The 1 dimensional transfer function is used in both 
// dimensions, so that if a point is added exactly at a grid point, it only contributes 9/16 to that point
// and 7/16 to the surrounding points following this matrix of contributions:
//
//   1/64   3/32   1/64
//   3/32   9/16   3/32 
//   1/64   3/32   1/64
//
// Pixel data is not kept in a 2D array -- 
// Rather, a dictionary is kept with the key being the PixelID
// This is memory efficient, because the grid is likely sparsely populated, and lookup
// efficient, as the PixelID is really represented by an integer
public class DensityPlane
{
    readonly float halfBinsize;
    public float binsize;
    readonly float oneOverBinsize;
    int oobPoints;
    readonly float maxval;
    readonly Dictionary<PixelID, float> data;
    
    public DensityPlane(float binSep)
    {
        binsize = binSep;
        halfBinsize = binSep * 0.5f; 
        oobPoints = 0;
        oneOverBinsize = 1.0f / binSep;
        data = new Dictionary<PixelID, float>();
        maxval = PixelID.Maxgrid * binSep;
    }

    public delegate void ForEachPixelCallback(PixelID pixelId, float value);

    public void ForEachPixel(ForEachPixelCallback callback)
    {
        foreach (KeyValuePair<PixelID, float> kvp in data)
        {
            callback(kvp.Key, kvp.Value);
        }
    }

    public DensityPlane Convolve(List<float> normalizedCoefficients)
    {
        DensityPlane newDP = new(this.binsize);
        if (normalizedCoefficients.Count > 0)
        {
            int maxx = normalizedCoefficients.Count - 1;
            int minx = -maxx;
            int maxy = maxx;
            int miny = minx;
            void convolutionFunction(PixelID pixelId, float value)
            {
                (int p, int q) = pixelId.XYCoords();
                for (int x = minx; x <= maxx; x+=1)
                {
                    int xCoefficientIndex = (int)Math.Abs(x);
                    float xCoeff = normalizedCoefficients[xCoefficientIndex];
                    for (int y = miny; y <= maxy; y += 1)
                    {
                        int yCoefficientIndex = (int)Math.Abs(y);
                        float yCoeff = normalizedCoefficients[yCoefficientIndex];
                        PixelID? nthPixelID = PixelID.PixelIDFor(p + x, q + y);
                        if (nthPixelID != null)
                        {  
                            newDP.AddValueAtPixel(nthPixelID.Value, value * xCoeff * yCoeff);
                        }
                    }
                }
            }
            ForEachPixel(convolutionFunction);
        }
        return newDP;
    }

    public void AddValueAtPixel(PixelID pixelId, float value)
    {
        data[pixelId] = ValueAtPixel(pixelId) + value;
    }

    public List<PixelID> GridPointIds()
    {
        return data.Keys.ToList();
    }

    internal void AddIfNonZero(int x, int y, List<PixelID> list)
    {
        PixelID? possibleBin = PixelID.PixelIDFor(x, y);
        if (possibleBin != null)
        {
            PixelID pixelId = possibleBin.Value;
            if (data.ContainsKey(pixelId))
            {
                list.Add(pixelId);
            }
              
        }
    }

    // check for the 8 neighboring pixels and add them if they have a non-zero value
    public List<PixelID> PixelIdsNeighboring(PixelID pixelId)
    {
        (int x, int y) = pixelId.XYCoords();
        List<PixelID> neighbors = new();
        AddIfNonZero(x - 1, y - 1, neighbors);
        AddIfNonZero(x - 1, y, neighbors);
        AddIfNonZero(x - 1, y + 1, neighbors);
        AddIfNonZero(x, y-1, neighbors);
        AddIfNonZero(x, y+1, neighbors);
        AddIfNonZero(x + 1, y-1, neighbors);
        AddIfNonZero(x + 1, y, neighbors);
        AddIfNonZero(x + 1, y + 1, neighbors);

		return neighbors;
	}
	
    public float MaximumValue(List<PixelID> candidates)
    {
        float maxScore = float.MinValue;

        foreach (PixelID candidate in candidates)
        {
            float nthScore = ValueAtPixel(candidate);
            if (nthScore > maxScore)
            {
                maxScore = nthScore;
            }
        }
        return maxScore;
    }
    
    public PixelID? FindMaximum(List<PixelID> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }
        PixelID bestCandidate = candidates[0];
        float currMax = ValueAtPixel(bestCandidate);
        foreach (PixelID candidate in candidates)
        {
            float nthPopulation = ValueAtPixel(candidate);
            if (nthPopulation > currMax)
            {
                currMax = nthPopulation;
                bestCandidate = candidate;
            }
        }
        return bestCandidate;
    }
    
    public (TwoDGridCoord, TwoDGridCoord) MinMaxGridCoords()
    {
        int miny = 0;
        int minx = 0;
        int maxy = 0;
        int maxx = 0;
        bool first = true;
      
        var pixelIds = data.Keys.ToList();
        pixelIds.Sort();
        foreach (PixelID pixelId in pixelIds)
        {
            (int x, int y) = pixelId.XYCoords();
            if (!first)
            {
                miny = Math.Min(y, miny);
                maxy = Math.Max(y, maxy);
                minx = Math.Min(x, minx);
                maxx = Math.Max(x, maxx);
            }
            else
            {
                miny = y;
                maxy = y;
                minx = x;
                maxx = x;
                first = false;
            }
            float binx = x * binsize;
            float biny = y * binsize;
            // Debug.WriteLine("x = " + binx + ",y = " + biny + ", population = " + data[key]);
        }
        return (new TwoDGridCoord(minx, miny), new TwoDGridCoord(maxx, maxy));
    }
    
    public void WriteToStream(StreamWriter stream)
    {
        TwoDGridCoord min;
        TwoDGridCoord max;
        (min, max) = MinMaxGridCoords();

        int xSize = 1 + max.x - min.x;
		int ySize = 1 + max.y - min.y;
		stream.WriteLine("x size= " + xSize);
		stream.WriteLine("y size= " + ySize);

		stream.Write("{ " );
		// now csv data for the grid from min to max
		for (int y = min.y; y <= max.y; ++y)
		{
			string l = "{";
			for (int x = min.x; x <= max.x; ++x)
			{
				PixelID? xthKey = PixelID.PixelIDFor(x, y);
                if (xthKey != null)
                {
                    PixelID xthPixelId = xthKey.Value;
                    float xthValue = 0;
                    if (data.ContainsKey(xthPixelId))
                    {
                        xthValue = data[xthPixelId];
                    }
                    l += xthValue;
                    if (x != max.x)
                    {
                        l += ",";
                    }
                }
			}
			l += "}";
			stream.Write(l);
			if (y != max.y)
			{
				stream.Write(",");
			}
		}
		stream.Write("}");
    }
    
    public void ConsoleDump(string prefix)
    {
        int miny = 0; 
        int minx = 0;
        int maxy = 0;
        int maxx = 0;
        bool first = true;
        Debug.WriteLine("DensityProfile Dump " + prefix);
        var pixelIds = data.Keys.ToList();
        pixelIds.Sort();
        foreach (PixelID pixelId in pixelIds)
        {
            (int x, int y) = pixelId.XYCoords();
            if (!first)
            {
                miny = Math.Min(y, miny);
                maxy = Math.Max(y, maxy); 
                minx = Math.Min(x, minx);
                maxx = Math.Max(x, maxx);
            }
            else
            {
                miny = y;
                maxy = y;
                minx = x;
                maxx = x;
                first = false;
            }
            float binx = x * binsize;
            float biny = y * binsize;
            Debug.WriteLine("x = " + binx + ",y = " + biny + ", population = " + data[pixelId]);
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

    public float ValueAtGridCoords(int x, int y)
    {
        PixelID? pixelId = PixelID.PixelIDFor(x, y);
        if (pixelId == null)
        {
            return 0.0f;
        }
        return this.ValueAtPixel(pixelId.Value);
    }

    public float ValueAtPixel(PixelID pixelId)
    {
        float binValue;
        if (data.TryGetValue(pixelId, out binValue))
        {
            return binValue;
        }
        return 0.0f;
    }

    public (int, int) PixelIndicesFor(float x, float y)
    {
        int xBinIndex = (int)MathF.Floor((x + halfBinsize) * oneOverBinsize);
        int yBinIndex = (int)MathF.Floor((y + halfBinsize) * oneOverBinsize);
        return (xBinIndex, yBinIndex);
    }

    public (float, float) XYCoordsFor(PixelID pixelId)
    {
        (int xBinIndex, int yBinIndex) = pixelId.XYCoords();
        return (xBinIndex * binsize, yBinIndex * binsize);
    }

    public PixelID? PixelIDFor(float x, float y)
    {
        int binx;
        int biny;
        (binx, biny) = PixelIndicesFor(x, y);
        return PixelID.PixelIDFor(binx, biny);
    }

    public void AddPointAt(float x, float y)
    {
        if ((Math.Abs(x) > maxval) || (Math.Abs(y) > maxval)) {
            oobPoints += 1;
            return;
        }

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

        void addValueAtCoords(int x, int y, float val)
        {
            PixelID? pixelId = PixelID.PixelIDFor(x, y);
            if (pixelId != null)
            {
                AddValueAtPixel(pixelId.Value, val);
            }
        }

        int xBinIndex;
        int yBinIndex;
        (xBinIndex, yBinIndex) = PixelIndicesFor(x, y);

        float xRem = (x - (xBinIndex * binsize)) * oneOverBinsize;
        float xm1 = (float)Neg1Func(xRem);
        float xp1 = (float)Pos1Func(xRem);    // these are the spline transfer functions
        float x0 = (float)ZeroFunc(xRem); 
       
        float yRem = (y - (yBinIndex * binsize)) * oneOverBinsize;
        float ym1 = (float)Neg1Func(yRem);
        float yp1 = (float)Pos1Func(yRem);    // these are the spline transfer functions
        float y0 = (float)ZeroFunc(yRem);

        addValueAtCoords(xBinIndex - 1, yBinIndex - 1, xm1 * ym1);
        addValueAtCoords(xBinIndex, yBinIndex - 1, x0 * ym1);
        addValueAtCoords(xBinIndex + 1, yBinIndex - 1, xp1 * ym1);
        addValueAtCoords(xBinIndex - 1, yBinIndex, xm1 * y0);
        addValueAtCoords(xBinIndex, yBinIndex, x0 * y0);
        addValueAtCoords(xBinIndex + 1, yBinIndex, xp1 * y0);
        addValueAtCoords(xBinIndex - 1, yBinIndex + 1, xm1 * yp1);
        addValueAtCoords(xBinIndex, yBinIndex + 1, x0 * yp1);
        addValueAtCoords(xBinIndex + 1, yBinIndex + 1, xp1 * yp1);
    }

    // IdentifyTwoDPartitions use the density map from the twoDGrid to separate 
    // voxels that belong to different peaks in the DensityPlane
    // first, identify the peaks and their associated pixels
    // then, for each voxel, see if it lands in on of the partitioned pixels.
    // If it does, add it to the appropriate list
    // return the list of lists
    // indices not identified are not returned in any list
    public List<List<PixelID>> IdentifyTwoDPartitions(PcaPhaseIdentificationProperties props)
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
        TwoDGridPartitionFinder partitionFinder = new(this, props);

        partitionFinder.FindPartitions(props.noiseFloorFraction);
        var pixelLists = partitionFinder.GetPixelLists();
        partitionFinder.Clear();
        return pixelLists;
    }
}



