using Cameca.CustomAnalysis.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
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
        this.oneDProjectionDelocalization = 0.05f; 
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
        nComponents = compResults.Components.Count;
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
        int nComponents = compResults.Components.Count;

        int x = compResults.Grid3DData.NumVoxels[0];
        int y = compResults.Grid3DData.NumVoxels[1];
        int z = compResults.Grid3DData.NumVoxels[2];

        ThreeDGridDimensions gridDimensions = new(x, y, z);
        return new PcaScoresGrid(this, compResults.VoxelIndices.Length, nComponents, gridDimensions);
    }
}


internal static class PcaCalculator
{
    /*
     * Per Mike Keenan regarding adjustments to scores and loadings:
     * The scores are O(10-3). The loadings in this case are O(1000).
     * The data matrix is modeled as a product of the scores and loadings.
     * So, if you multiply the scores by 1000 and divide the loadings by 1000,
     * both factors will be O(1), which is more in line with the expected magnitude of data-matrix elements.
     * This would probably be a good thing to do.
     */
    private const float ScoresCoefficient = 1000f;
    private const float LoadingsCoefficient = 0.001f;

    public static EigenvalueResults GetEignevalues(IIonData ionData, IGrid3DData gridData, int nFeatures)
    {
        int nAllVoxels = gridData.NumVoxels[0] * gridData.NumVoxels[1] * gridData.NumVoxels[2];

        var localBuffer = Enumerable.Range(0, nFeatures)
            .Select(ionIndex => GetNormalizedIonCount(gridData.GetDataForIon(ionIndex)))
            .ToArray();

        var nonEmptyVoxels = new List<int>();
        for (int voxelIndex = 0; voxelIndex < nAllVoxels; voxelIndex++)
        {
            for (int ionIndex = 0; ionIndex < nFeatures; ionIndex++)
            {
                if (localBuffer[ionIndex][voxelIndex] != 0f)
                {
                    nonEmptyVoxels.Add(voxelIndex);
                    break;
                }
            }
        }
        int nVoxels = nonEmptyVoxels.Count;

        var dataBuffer = new float[nFeatures * nVoxels];
        for (int featureIndex = 0; featureIndex < nFeatures; featureIndex++)
        {
            int x = 0;
            foreach (int voxelIndex in nonEmptyVoxels)
            {
                dataBuffer[(featureIndex * nVoxels) + x++] = localBuffer[featureIndex][voxelIndex];
            }
        }

        int nevals = nFeatures;  // Input?

        float[] evals = new float[nevals];

        PcaLib.doEigen(nVoxels, nFeatures, dataBuffer, nevals, evals);

        return new EigenvalueResults(evals);
    }

    private static float[] GetNormalizedIonCount(ReadOnlyMemory<float> buffer)
    {
        return buffer.ToArray();
    }
 
    public static PcaScoresGrid GenerateScoresGrid(IIonData ionData, ComponentsResults compResults, PcaPhaseIdentificationProperties properties)
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

    public static NoiseEigenvalueResults GetNoiseEigenvalues(float[] evals, int gaps, int significance, bool refine)
    {
        int nevals = evals.Length;
        int rank = PcaLib.EstimateRankF(evals, nevals, nevals, gaps, significance, refine);

        float[] noiseEvals = new float[nevals - rank];
        PcaLib.NoiseEvals(rank, nevals, nevals, noiseEvals);
        return new NoiseEigenvalueResults(rank, noiseEvals);
    }

    public static ComponentsResults GetComponents(IGrid3DData gridData, IIonData ionData, int nFeatures, int nComponents)
    {
        // Remove empty voxels
        int nAllVoxels = gridData.NumVoxels[0] * gridData.NumVoxels[1] * gridData.NumVoxels[2];

        var localBuffer = Enumerable.Range(0, nFeatures)
            .Select(ionIndex => GetNormalizedIonCount(gridData.GetDataForIon(ionIndex)))
            .ToArray();

        var nonEmptyVoxels = new List<int>();
        for (int voxelIndex = 0; voxelIndex < nAllVoxels; voxelIndex++)
        {
            for (int ionIndex = 0; ionIndex < nFeatures; ionIndex++)
            {
                if (localBuffer[ionIndex][voxelIndex] != 0f)
                {
                    nonEmptyVoxels.Add(voxelIndex);
                    break;
                }
            }
        }
        int nVoxels = nonEmptyVoxels.Count;

        var dataBuffer = new float[nFeatures * nVoxels];
        for (int featureIndex = 0; featureIndex < nFeatures; featureIndex++)
        {
            int x = 0;
            foreach (int voxelIndex in nonEmptyVoxels)
            {
                dataBuffer[(featureIndex * nVoxels) + x++] = localBuffer[featureIndex][voxelIndex];
            }
        }

        int nevals = nFeatures;  // Input?


        // Allocated output buffers
        float[] scores = new float[nVoxels * nComponents];
        float[] loads = new float[nFeatures * nComponents];
        float[] evals = new float[nevals];

        // Call the doPCA function
        PcaLib.doPCA(nVoxels, nFeatures, dataBuffer, nComponents, nevals, scores, loads, evals);

        // Normalization
        for (int i = 0; i < scores.Length; i++)
        {
            scores[i] *= ScoresCoefficient;
        }
        for (int i = 0; i < loads.Length; i++)
        {
            loads[i] *= LoadingsCoefficient;
        }

        var components = new List<ComponentResults>(nComponents);
        for (int i = 0; i < nComponents; i++)
        {
            var compScores = scores.Skip(i * nVoxels).Take(nVoxels).ToArray();
            var compLoads = loads.Skip(i * nFeatures).Take(nFeatures).ToArray();
            components.Add(new ComponentResults(compScores, compLoads));
        }
        return new ComponentsResults(gridData, nonEmptyVoxels.ToArray(), components);
    }
}
