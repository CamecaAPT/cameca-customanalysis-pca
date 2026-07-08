#include "pch.h"
#include <Eigen/Dense>
#include "VoxelFeatureMatrixImpl.h"
#include "GaussianMixtureModel.h"
#include "GMM.h"

using namespace Eigen;
using namespace Cameca::CustomAnalysis::PcaLib;

GMMResults GaussianMixtureModel::TrainModel(const int nClust, const int nReplicates, const float regParam) const
{
    const int nVoxels = matrix->GetVoxelCount();
    // I thinks nSamples from the example is the voxel count here (nVoxels)
    // Consolidate after confirmation
    const int nSamples = nVoxels;
    const int nFeatures = matrix->GetFeatureCount();

    // Construct the data matrix
    const MatrixXf X = matrix->GetMatrix().transpose().eval();

    MatrixXf Centroid(nFeatures, nClust);
    MatrixXf postP(nSamples, nClust);
    VectorXi clustID(nSamples);
    MatrixXf mu(nFeatures, nClust);
    MatrixXf Sigma(nFeatures, nClust * nFeatures);


    float cost = trainModel(X, nClust, nReplicates, regParam, Centroid, postP, clustID, mu, Sigma);

    std::vector<int> voxelIndex(clustID.data(), clustID.data() + clustID.size());


    return GMMResults{ cost, voxelIndex };
}