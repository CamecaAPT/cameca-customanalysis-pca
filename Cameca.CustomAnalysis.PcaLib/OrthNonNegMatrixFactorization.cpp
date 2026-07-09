#include "pch.h"
#include <Eigen/Dense>
#include "VoxelFeatureMatrixImpl.h"
#include "OrthNonNegMatrixFactorization.h"
#include "ONMF.h"

using namespace Eigen;
using namespace Cameca::CustomAnalysis::PcaLib;

ONMFResults OrthNonNegMatrixFactorization::TrainModel(const int nClust, const int nReplicates, const bool weighted) const
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
    MatrixXf H(nSamples, nClust);

    MatrixXf transX = X.transpose().eval();
    float cost = trainModel(transX, nClust, nReplicates, Centroid, H, indx, weighted);

    std::vector<int> voxelIndex(indx.data(), indx.data() + indx.size());

    return ONMFResults { cost, voxelIndex };
}