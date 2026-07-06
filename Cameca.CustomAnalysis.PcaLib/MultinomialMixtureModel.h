#pragma once
#include "PcaLibExport.h"
#include "VoxelFeatureMatrix.h"

namespace Cameca::CustomAnalysis::PcaLib {

	struct MnMMResults
	{
		float cost;
		std::vector<int> voxelIndex;
	};

	class PCALIB_API MultinomialMixtureModel
	{
	public:
		MultinomialMixtureModel(std::shared_ptr<const VoxelFeatureMatrixImpl> matrix)
			: matrix(std::move(matrix)) {
		}
		virtual ~MultinomialMixtureModel() = default;

		MnMMResults TrainModel(const int nClust, const int nReplicates) const;

	private:
		std::shared_ptr<const VoxelFeatureMatrixImpl> matrix;
	};
}