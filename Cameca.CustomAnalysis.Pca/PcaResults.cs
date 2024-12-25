namespace Cameca.CustomAnalysis.Pca;

internal record PcaResults(float[] Scores, int[] VoxelIndices, float[] Loads, float[] Evals, int[] NumVoxels, double[] VoxelSize, double[] GridDelta);