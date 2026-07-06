#pragma once
#include "PcaLibExport.h"
#include "VoxelFeatureMatrix.h"

namespace Cameca::CustomAnalysis::PcaLib {

	struct NegMnMMResults
	{
		float cost;
		std::vector<int> voxelIndex;
	};

	class PCALIB_API NegMultinomialMixtureModel
	{
	public:
		NegMultinomialMixtureModel(std::shared_ptr<const VoxelFeatureMatrixImpl> matrix)
			: matrix(std::move(matrix)) {
		}
		virtual ~NegMultinomialMixtureModel() = default;

		NegMnMMResults TrainModel(const int nClust, const int nReplicates) const;

	private:
		std::shared_ptr<const VoxelFeatureMatrixImpl> matrix;
	};
}