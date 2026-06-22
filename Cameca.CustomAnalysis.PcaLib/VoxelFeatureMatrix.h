#pragma once
#include "PcaLibExport.h"
#include <memory>
#include <vector>

namespace Cameca::CustomAnalysis::PcaLib {
	// Forward declaration
	class VoxelFeatureMatrixImpl;

	class PCALIB_API VoxelFeatureMatrix
	{
	public:
		VoxelFeatureMatrix(std::shared_ptr<const VoxelFeatureMatrixImpl> impl);
		~VoxelFeatureMatrix();

		static VoxelFeatureMatrix FromData(
			const float* matrixData,
			int rows,
			int cols,
			const int* voxelIndicesData,
			int voxelIndicesLength);


		VoxelFeatureMatrix(const VoxelFeatureMatrix&) = delete;
		VoxelFeatureMatrix& operator=(const VoxelFeatureMatrix&) = delete;
		VoxelFeatureMatrix(VoxelFeatureMatrix&&) noexcept;
		VoxelFeatureMatrix& operator=(VoxelFeatureMatrix&&) noexcept;

		const int GetVoxelCount() const;
		const int GetFeatureCount() const;
		const float* GetData() const;
		const int GetDataLength() const;

		const std::vector<int> GetVoxelIndices() const;

		std::shared_ptr<const VoxelFeatureMatrixImpl> pImpl;
	};
}
