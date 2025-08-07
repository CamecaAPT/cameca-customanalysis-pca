#include "pch.h"
#include "VoxelFeatureMatrix.h"
#include "VoxelFeatureMatrixImpl.h"
#include <vector>

using namespace Cameca::CustomAnalysis::PcaLib;

VoxelFeatureMatrix::VoxelFeatureMatrix(std::shared_ptr<const VoxelFeatureMatrixImpl> impl)
	: pImpl(std::move(impl)) {}
VoxelFeatureMatrix::~VoxelFeatureMatrix() = default;

VoxelFeatureMatrix::VoxelFeatureMatrix(VoxelFeatureMatrix&&) noexcept = default;
VoxelFeatureMatrix& VoxelFeatureMatrix::operator=(VoxelFeatureMatrix&&) noexcept = default;

const int VoxelFeatureMatrix::GetVoxelCount() const { return pImpl->GetVoxelCount(); }
const int VoxelFeatureMatrix::GetFeatureCount() const { return pImpl->GetFeatureCount(); }

const std::vector<int> VoxelFeatureMatrix::GetVoxelIndices() const { return pImpl->GetVoxelIndices(); }