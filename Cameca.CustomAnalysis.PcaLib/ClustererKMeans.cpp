#include "pch.h"
#include <Eigen/Dense>
#include "VoxelFeatureMatrixImpl.h"
#include "ClustererKMeans.h"
#include "kmeans.h"

using namespace Eigen;
using namespace Cameca::CustomAnalysis::PcaLib;

ClusterData ClustererKMeans::Cluster(const int nClust, const int nReplicates, const bool weighted) const
{
    const int nVoxels = matrix->GetVoxelCount();
    // I thinks nSamples from the example is the voxel count here (nVoxels)
    // Consolidate after confir-mation
    const int nSamples = nVoxels;
    const int nFeatures = matrix->GetFeatureCount();

    // Construct the data matrix
    const MatrixXf X = matrix->GetMatrix();

    MatrixXf Centroid(nFeatures, nClust);
    VectorXi indx(nSamples);

    float cost;
    // Weighted
    if (has_loads && loads.size() > 0) {
        // All rows will be same length
        int rows = loads[0].size();
        int cols = nFeatures;
        MatrixXf P(rows, cols);
        for (int i = 0; i < rows; ++i) {
            P.row(i) = Map<const RowVectorXf>(loads[i].data(), loads[i].size());
        }

        cost = trainModel(X, P, nClust, nReplicates, Centroid, indx, weighted);
    }
    // Standard
    else {
        MatrixXf transX = X.transpose().eval();
        cost = trainModel(transX, nClust, nReplicates, Centroid, indx, weighted);
    }

    std::vector<int> voxelIndex(indx.data(), indx.data() + indx.size());


    return ClusterData{ cost, voxelIndex };
}