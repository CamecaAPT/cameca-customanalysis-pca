#include "pch.h"
#include "VoxelFeatureMatrix.h"
#include "VoxelFeatureMatrixImpl.h"
#include <vector>

using namespace Cameca::CustomAnalysis::PcaLib;

VoxelFeatureMatrix::VoxelFeatureMatrix(std::shared_ptr<const VoxelFeatureMatrixImpl> impl)
	: pImpl(std::move(impl)) {}
VoxelFeatureMatrix::~VoxelFeatureMatrix() = default;

VoxelFeatureMatrix VoxelFeatureMatrix::FromData(
	const float* matrixData,
	int rows,
	int cols,
	const int* voxelIndicesData,
	int voxelIndicesLength)
{
	Eigen::Map<const Eigen::MatrixXf> matrix(matrixData, rows, cols);
	std::vector<int> voxelIndices(voxelIndicesData, voxelIndicesData + voxelIndicesLength);
	auto impl = std::make_shared<VoxelFeatureMatrixImpl>(std::move(matrix), std::move(voxelIndices));
	return VoxelFeatureMatrix(std::move(impl));
}

VoxelFeatureMatrix::VoxelFeatureMatrix(VoxelFeatureMatrix&&) noexcept = default;
VoxelFeatureMatrix& VoxelFeatureMatrix::operator=(VoxelFeatureMatrix&&) noexcept = default;

const int VoxelFeatureMatrix::GetVoxelCount() const { return pImpl->GetVoxelCount(); }
const int VoxelFeatureMatrix::GetFeatureCount() const { return pImpl->GetFeatureCount(); }
const float* VoxelFeatureMatrix::GetData() const {
	if (!pImpl) return nullptr;
	return pImpl->GetMatrix().data();
}
const int VoxelFeatureMatrix::GetDataLength() const { return pImpl->GetDataLength(); }

const std::vector<int> VoxelFeatureMatrix::GetVoxelIndices() const { return pImpl->GetVoxelIndices(); }