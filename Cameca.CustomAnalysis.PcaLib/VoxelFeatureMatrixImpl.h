#pragma once
#include <Eigen/Dense>

namespace Cameca::CustomAnalysis::PcaLib
{
	class VoxelFeatureMatrixImpl
	{
	public:
		VoxelFeatureMatrixImpl(Eigen::MatrixXf&& matrix, std::vector<int>&& voxelIndices)
			: matrix(std::move(matrix)), voxelIndices(std::move(voxelIndices)) { }

		const int GetVoxelCount() const { return static_cast<int>(matrix.rows()); }
		const int GetFeatureCount() const { return static_cast<int>(matrix.cols()); }
		
		const Eigen::MatrixXf& GetMatrix() const { return matrix; }
		const std::vector<int> GetVoxelIndices() const { return voxelIndices; }
	private:
		// Non-empty Voxel x Features matrix
		const Eigen::MatrixXf matrix;
		// Length of non-empty voxel: each value maps matrix row index to it's original grid voxel index
		const std::vector<int> voxelIndices;
	};
}

