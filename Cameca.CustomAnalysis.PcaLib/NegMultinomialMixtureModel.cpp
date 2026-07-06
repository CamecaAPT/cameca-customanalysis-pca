#include "pch.h"
#include "pch.h"
#include <Eigen/Dense>
#include "VoxelFeatureMatrixImpl.h"
#include "NegMultinomialMixtureModel.h"
#include "negmultinomialMM.h"

using namespace Eigen;
using namespace Cameca::CustomAnalysis::PcaLib;

NegMnMMResults NegMultinomialMixtureModel::TrainModel(const int nClust, const int nReplicates) const
{
    const int nVoxels = matrix->GetVoxelCount();
    // I thinks nSamples from the example is the voxel count here (nVoxels)
    // Consolidate after confirmation
    const int nSamples = nVoxels;
    const int nFeatures = matrix->GetFeatureCount();

    // Construct the data matrix
    const MatrixXf X = matrix->GetMatrix().transpose().eval();

    MatrixXf Centroid(nFeatures, nClust);
    VectorXi indx(nSamples);
    MatrixXf postP(nSamples, nClust);
    MatrixXf fraction(nClust, 1);
    MatrixXf NMr(nClust, 1);
    MatrixXf NMp(nFeatures + 1, nClust);

    float cost = trainModel(X, nClust, nReplicates, Centroid, indx, postP, fraction, NMr, NMp);

    std::vector<int> voxelIndex(indx.data(), indx.data() + indx.size());


    return NegMnMMResults{ cost, voxelIndex };
}