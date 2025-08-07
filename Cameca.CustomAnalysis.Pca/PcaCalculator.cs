using Cameca.CustomAnalysis.Pca.Models;
using Cameca.CustomAnalysis.Pca.VoxelLogic;

namespace Cameca.CustomAnalysis.Pca;


public delegate float[] GetScoresDelegate(int voxelIndex);

public struct PcaPhaseIdentificationProperties
{
    public float oneDProjectionBinSize;
    public float oneDProjectionDelocalization; 
    public float gridProjectionBinSize;
    public float gridProjectionDelocalization;
    public float noiseFloorFraction;
    public float peakSummitAllowance;
    public int numDimsForPCAPhaseId;

    public PcaPhaseIdentificationProperties(float gridProjectionBinSize,
          float gridProjectionDelocalization,
          float noiseFloor, 
          float peakSummitAllowance, 
          int numDimsForPCAPhaseId)
    {
        this.oneDProjectionBinSize = 0.05f;
        this.oneDProjectionDelocalization = gridProjectionDelocalization; 
        this.gridProjectionBinSize = gridProjectionBinSize;
        this.gridProjectionDelocalization = gridProjectionDelocalization;
        this.noiseFloorFraction = noiseFloor;
        this.peakSummitAllowance = peakSummitAllowance;
        this.numDimsForPCAPhaseId = numDimsForPCAPhaseId;
    }
}

public class PcaScoresGridProducer: IScoresProvider {

    readonly ComponentsResults compResults;
    readonly PcaPhaseIdentificationProperties properties;
    readonly int nComponents;

    public PcaScoresGridProducer(ComponentsResults results, PcaPhaseIdentificationProperties props)
    {
        compResults = results;
        properties = props;
        nComponents = compResults.Components.Length;
    }

    public (VoxelID, float[]) GetIDAndScores(int voxelIndex)
    {
        float[] scores = new float[nComponents];
        for (int i = 0; i < nComponents; i++)
        {
            scores[i] = compResults.Components[i].Scores[voxelIndex];
        }
        VoxelID voxelId = new(compResults.VoxelIndices[voxelIndex]);
        return (voxelId, scores);
    }

    public PcaScoresGrid GenerateScoresGrid()
    {
        int nComponents = compResults.Components.Length;

        int x = compResults.GridParams.VoxelCount[0];
        int y = compResults.GridParams.VoxelCount[1];
        int z = compResults.GridParams.VoxelCount[2];

        ThreeDGridDimensions gridDimensions = new(x, y, z);
        return new PcaScoresGrid(this, compResults.VoxelIndices.Length, nComponents, gridDimensions);
    }
}


internal static class PcaCalculator
{ 
    public static PcaScoresGrid GenerateScoresGrid(ComponentsResults compResults, PcaPhaseIdentificationProperties properties)
    {
        PcaScoresGridProducer producer = new(compResults, properties);

        return producer.GenerateScoresGrid();
    }

    public static TwoDGridsResults CalculateTwoDGrids(PcaScoresGrid scoresGrid, PcaPhaseIdentificationProperties properties)
    {
        return scoresGrid.CalculateTwoDGrids(properties);
    }

    public static OneDGridsResults CalculateOneDGrids(PcaScoresGrid scoresGrid, PcaPhaseIdentificationProperties properties)
    {
        return scoresGrid.CalculateOneDGrids(properties);
    }
}
