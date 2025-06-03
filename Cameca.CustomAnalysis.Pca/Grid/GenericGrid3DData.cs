using Cameca.CustomAnalysis.Interface;
using CommunityToolkit.HighPerformance;
using System;

namespace Cameca.CustomAnalysis.Pca.Grid;

/// <summary>
/// Generic implementation of <see cref="IGrid3DData"/>. The <see cref="IGrid3DData.GetDataForIon(int)"/> is
/// abstracted to grid channels instead of just ions, so the method should be read as GetDataForChannel(int),
/// and the number of channels should be tracked independantly, it is not guarenteed to be the same as
/// <see cref="IIonData.Ions"/> length.
/// 
/// The purpose of reusing the <see cref="IGrid3DData"/> interface is to minimize changes to the existing
/// code that utilized the <see cref="IGrid3DData"/> instace provided by the host application.
/// </summary>
public class GenericGrid3DData : IGrid3DData
{
    public int NumChannels => data.GetLength(0);

    /// <summary>
    /// Array with count of voxels in each dimension { X, Y, Z }. Exactly 3 elements.
    /// </summary>
    public int[] NumVoxels { get; }

    /// <summary>
    /// Array with size (nm) of voxels in each dimension { X, Y, Z }. Exactly 3 elements.
    /// </summary>
    public double[] VoxelSize { get; }

    /// <summary>
    /// Array with total length (nm) of each grid dimension { X, Y, Z}. Exactly 3 elements.
    /// </summary>
    public double[] GridDelta { get; }

    /// <summary>
    /// 2D Array with min and max positions of grid { { X_min, X_max }, { Y_min, Y_max }, { Z_min, Z_max } }.
    /// </summary>
    public double[,] GridRange { get; }

    /// <summary>
    /// 2D array of flattened grid data for each channel. Each row is a channel, each column is a flattened 3D array in X,Y,Z order of each voxel value
    /// </summary>
    private readonly float[,] data;

    public ReadOnlyMemory<float> GetDataForIon(int ionIndex)
    {
        return data.GetRowMemory(ionIndex);
    }

    public GenericGrid3DData(int[] numVoxels, double[] voxelSize, double[] gridDelta, double[,] gridRange, float[,] data)
    {
        NumVoxels = numVoxels;
        VoxelSize = voxelSize;
        GridDelta = gridDelta;
        GridRange = gridRange;
        this.data = data;
    }
}