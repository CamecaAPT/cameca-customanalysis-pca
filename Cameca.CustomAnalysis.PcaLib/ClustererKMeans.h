#pragma once
#include "PcaLibExport.h"
#include "VoxelFeatureMatrix.h"
#include <vector>
#include "ClusterData.h"

namespace Cameca::CustomAnalysis::PcaLib {

	class PCALIB_API ClustererKMeans
	{
	public:
		ClustererKMeans(std::shared_ptr<const VoxelFeatureMatrixImpl> matrix)
			: matrix(std::move(matrix)), has_loads(false) { }
		ClustererKMeans(std::shared_ptr<const VoxelFeatureMatrixImpl> matrix, std::vector<std::vector<float>> loads)
		: matrix(std::move(matrix)), loads(std::move(loads)), has_loads(true) { }
		virtual ~ClustererKMeans() = default;

		ClusterData Cluster(const int nClust, const int nReplicates, const bool weighted) const;

	private:
		std::shared_ptr<const VoxelFeatureMatrixImpl> matrix;
		std::vector<std::vector<float>> loads;
		bool has_loads;
	};
}
