#pragma once
#include "PcaLibExport.h"
#include "VoxelFeatureMatrix.h"

namespace Cameca::CustomAnalysis::PcaLib {

	struct GMMResults
	{
		float cost;
		std::vector<int> voxelIndex;
	};

	class PCALIB_API GaussianMixtureModel
	{
	public:
		GaussianMixtureModel(std::shared_ptr<const VoxelFeatureMatrixImpl> matrix)
			: matrix(std::move(matrix)) {
		}
		virtual ~GaussianMixtureModel() = default;

		GMMResults TrainModel(const int nClust, const int nReplicates, const float regParam) const;

	private:
		std::shared_ptr<const VoxelFeatureMatrixImpl> matrix;
	};
}