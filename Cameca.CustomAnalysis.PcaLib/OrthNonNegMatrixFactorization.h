#pragma once
#pragma once
#include "PcaLibExport.h"
#include "VoxelFeatureMatrix.h"

namespace Cameca::CustomAnalysis::PcaLib {

	struct ONMFResults
	{
		float cost;
		std::vector<int> voxelIndex;
	};

	class PCALIB_API OrthNonNegMatrixFactorization
	{
	public:
		OrthNonNegMatrixFactorization(std::shared_ptr<const VoxelFeatureMatrixImpl> matrix)
			: matrix(std::move(matrix)) {
		}
		virtual ~OrthNonNegMatrixFactorization() = default;

		ONMFResults TrainModel(const int nClust, const int nReplicates, const bool weighted) const;

	private:
		std::shared_ptr<const VoxelFeatureMatrixImpl> matrix;
	};
}