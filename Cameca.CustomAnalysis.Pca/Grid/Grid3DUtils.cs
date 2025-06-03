using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Pca.Grid;
using Cameca.CustomAnalysis.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Cameca.CustomAnalysis.Pca;


internal record GridParameters(double[,] GridRange, double[] GridDelta, int[] VoxelCount);

internal class Grid3DUtils
{
    public static async Task<GenericGrid3DData?> CreateIonGrid3DData(IResources resources, IIonData ionData, double[] voxelSize, double edgeBuffer = 1.5d, CancellationToken cancellationToken = default)
    {
        var sections = new string[] { IonDataSectionName.Position, IonDataSectionName.IonType };
        bool sectionsAvailable = await resources.EnsureRequiredSectionsAvailable(ionData, sections, cancellationToken: cancellationToken);
        if (!sectionsAvailable) { return null; }

        var gridParams = CreateGridParameters(ionData.Extents, voxelSize, edgeBuffer);

        float[,] data = CreateData(ionData, voxelSize, gridParams, sections);

        return new GenericGrid3DData(
            gridParams.VoxelCount,
            voxelSize,
            gridParams.GridDelta,
            gridParams.GridRange,
            data);
    }
    public static async Task<GenericGrid3DData?> CreatePeakGrid3DData(IResources resources, IIonData ionData, double[] voxelSize, double edgeBuffer = 1.5d, CancellationToken cancellationToken = default)
    {
        var ionRanges = resources.RangeManager?.GetIonRanges();
        if (ionRanges is null) { return null; }

        var sections = new string[] { IonDataSectionName.Position, IonDataSectionName.Mass };
        bool sectionsAvailable = await resources.EnsureRequiredSectionsAvailable(ionData, sections, cancellationToken: cancellationToken);
        if (!sectionsAvailable) { return null; }

        var gridParams = CreateGridParameters(ionData.Extents, voxelSize, edgeBuffer);

        float[,] data = CreateData(ionData, ionRanges, voxelSize, gridParams, sections);

        return new GenericGrid3DData(
            gridParams.VoxelCount,
            voxelSize,
            gridParams.GridDelta,
            gridParams.GridRange,
            data);
    }

    private static float[,] CreateData(IIonData ionData, double[] voxelSize, GridParameters gridParams, string[] sections)
    {
        int channelCount = ionData.Ions.Count();

        int voxelsX = gridParams.VoxelCount[0];
        int voxelsY = gridParams.VoxelCount[1];
        int voxelsAll = voxelsX * voxelsY * gridParams.VoxelCount[2];

        float[,] data = new float[channelCount, voxelsAll];
        foreach (var chunk in ionData.CreateSectionDataEnumerable(sections))
        {
            var positions = chunk.ReadSectionData<Vector3>(IonDataSectionName.Position).Span;
            var ionTypes = chunk.ReadSectionData<byte>(IonDataSectionName.IonType).Span;

            for (var i = 0; i < chunk.Length; i++)
            {
                // Unranged Ion
                if (ionTypes[i] == byte.MaxValue) { continue; }

                var position = positions[i];

                var voxX = (int)Math.Floor((position.X - gridParams.GridRange[0, 0]) / voxelSize[0]);
                var voxY = (int)Math.Floor((position.Y - gridParams.GridRange[1, 0]) / voxelSize[1]);
                var voxZ = (int)Math.Floor((position.Z - gridParams.GridRange[2, 0]) / voxelSize[2]);

                var voxIndex = voxX + (voxY * voxelsX) + (voxZ * voxelsX * voxelsY);

                data[ionTypes[i], voxIndex] += 1;
            }
        }
        return data;
    }

    private readonly struct RangeMapEntry
    {
        public readonly double Min;
        public readonly double Max;
        public readonly byte Index;
        public RangeMapEntry(double min, double max, byte index)
        {
            this.Min = min;
            this.Max = max;
            this.Index = index;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte GetIndex(List<RangeMapEntry> rangeMap, float mass)
    {
        for (int r = 0; r < rangeMap.Count; r++)
        {
            var entry = rangeMap[r];
            if (mass >= entry.Min && mass < entry.Max)
            {
                return entry.Index;
            }
        }
        return byte.MaxValue;
    }

    private static float[,] CreateData(IIonData ionData, IEnumerable<IonTypeInfoRange> ionRanges, double[] voxelSize, GridParameters gridParams, string[] sections)
    {
        var rangeMap = ionRanges.Select((r, i) => new RangeMapEntry(r.Min, r.Max, (byte)i)).ToList();

        int channelCount = ionRanges.Count();

        int voxelsX = gridParams.VoxelCount[0];
        int voxelsY = gridParams.VoxelCount[1];
        int voxelsAll = voxelsX * voxelsY * gridParams.VoxelCount[2];

        float[,] data = new float[channelCount, voxelsAll];
        foreach (var chunk in ionData.CreateSectionDataEnumerable(sections))
        {
            var positions = chunk.ReadSectionData<Vector3>(IonDataSectionName.Position).Span;
            var masses = chunk.ReadSectionData<float>(IonDataSectionName.Mass).Span;

            for (var i = 0; i < chunk.Length; i++)
            {
                // Check if in assigned peak range
                float mass = masses[i];

                var index = GetIndex(rangeMap, masses[i]);

                if (index == byte.MaxValue) { continue; }

                var position = positions[i];

                var voxX = (int)Math.Floor((position.X - gridParams.GridRange[0, 0]) / voxelSize[0]);
                var voxY = (int)Math.Floor((position.Y - gridParams.GridRange[1, 0]) / voxelSize[1]);
                var voxZ = (int)Math.Floor((position.Z - gridParams.GridRange[2, 0]) / voxelSize[2]);

                var voxIndex = voxX + (voxY * voxelsX) + (voxZ * voxelsX * voxelsY);

                data[index, voxIndex] += 1;
            }
        }
        return data;
    }

    /// <summary>
    /// Calculate voxel counts and grid ranges 
    /// </summary>
    /// <remarks>
    /// AP Suite grid requirements: match this logic for consistency in extension + host application 3D grid generation
    /// <br />
    /// - Add an edge buffer to allow for less bounds checking in kernal distribution and exterior suface generation on fill-missing
    /// <br />
    /// - Odd number of voxels for delocalization kernal that has an odd number of voxels to be able to be used in place
    /// </remarks>
    /// <param name="roiExtents"></param>
    /// <param name="voxelSize"></param>
    /// <param name="edgeBuffer"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static GridParameters CreateGridParameters(Extents roiExtents, double[] voxelSize, double edgeBuffer = 1.5d)
    {
        if (edgeBuffer < 0d) throw new ArgumentOutOfRangeException(nameof(edgeBuffer), edgeBuffer, $"{nameof(edgeBuffer)} must be non-negative value");

        double[][] extents = new double[][]
        {
        new double[] { roiExtents.Min.X, roiExtents.Max.X },
        new double[] { roiExtents.Min.Y, roiExtents.Max.Y },
        new double[] { roiExtents.Min.Z, roiExtents.Max.Z },
        };
        double[,] gridRange = new double[3, 2];
        double[] gridDelta = new double[3];
        int[] voxelCount = new int[3];

        // Calculate voxel counts and grid ranges
        // AP Suite grid requirements: match this logic for consistency in extension + host application 3D grid generation
        // - Add an edge buffer to allow for less bounds checking in kernal distribution and exterior suface generation on fill-missing
        // - Odd number of voxels for delocalization kernal that has an odd number of voxels to be able to be used in place
        for (int i = 0; i < 3; i++)
        {
            double delta = extents[i][1] - extents[i][0];
            double center = extents[i][1] - delta / 2d;
            double halfCount = Math.Ceiling((delta / 2d) / voxelSize[i]) + edgeBuffer;

            gridRange[i, 0] = center - halfCount * voxelSize[i];
            gridRange[i, 1] = center + halfCount * voxelSize[i];
            gridDelta[i] = gridRange[i, 1] - gridRange[i, 0];
            voxelCount[i] = (int)(2.0 * halfCount + 0.5);
        }
        return new GridParameters(gridRange, gridDelta, voxelCount);
    }
}

