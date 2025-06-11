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

		VoxelFeatureMatrix(const VoxelFeatureMatrix&) = delete;
		VoxelFeatureMatrix& operator=(const VoxelFeatureMatrix&) = delete;
		VoxelFeatureMatrix(VoxelFeatureMatrix&&) noexcept;
		VoxelFeatureMatrix& operator=(VoxelFeatureMatrix&&) noexcept;

		const int GetVoxelCount() const;
		const int GetFeatureCount() const;

		const std::vector<int> GetVoxelIndices() const;

		std::shared_ptr<const VoxelFeatureMatrixImpl> pImpl;
	};
}
