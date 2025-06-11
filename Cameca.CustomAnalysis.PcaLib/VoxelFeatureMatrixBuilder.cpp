#include "pch.h"
#include "VoxelFeatureMatrix.h"
#include "VoxelFeatureMatrixBuilder.h"
#include "VoxelFeatureMatrixIon.h"
#include <memory>
#include <span>
#include <tbb/parallel_for.h>
#include <tbb/enumerable_thread_specific.h>
#include <tbb/blocked_range.h>
#include <Eigen/Dense>
#include "VoxelFeatureMatrixImpl.h"

using namespace Cameca::CustomAnalysis::PcaLib;

void VoxelFeatureMatrixBuilder::Update(std::span<VoxelFeatureMatrixIon> matrixIonData)
{
	std::copy(matrixIonData.begin(), matrixIonData.end(), allMatrixIonData.begin() + offset);
	offset += matrixIonData.size();
}

VoxelFeatureMatrix VoxelFeatureMatrixBuilder::Build() {

	auto length = allMatrixIonData.size();

	using GridIndexType = decltype(VoxelFeatureMatrixIon::Voxel);
	using FeatureType = decltype(VoxelFeatureMatrixIon::Feature);

	// Thread-local sets and max feature trackers
	std::vector<bool>voxelUsed(voxelCount);
	tbb::enumerable_thread_specific<FeatureType> localMaxFeatures(-1);

	tbb::parallel_for(tbb::blocked_range<std::size_t>(0, length),
		[&](tbb::blocked_range<std::size_t> r) {
			auto& localMax = localMaxFeatures.local();
			for (std::size_t i = r.begin(); i < r.end(); ++i) {
				auto feature = allMatrixIonData[i].Feature;
				if (feature >= 0) {
					voxelUsed[allMatrixIonData[i].Voxel] = true;
					if (feature > localMax) {
						localMax = feature;
					}
				}
			}
		}
	);

	// Reduce max feature
	FeatureType nFeatures = -1;
	for (const auto& localMax : localMaxFeatures) {
		if (localMax > nFeatures) {
			nFeatures = localMax;
		}
	}

	// Features are indices, so add 1 to the max value to get the count
	nFeatures += 1;

	// Compressed voxels
	GridIndexType compressedIndex = 0;
	std::vector<GridIndexType>voxelToIndex(voxelCount);
	std::vector<GridIndexType> indexToVoxel;
	for (int i = 0; i < voxelCount; ++i) {
		if (voxelUsed[i]) {
			indexToVoxel.push_back(i);
			voxelToIndex[i] = compressedIndex++;
		}
	}
	int nVoxels = compressedIndex;

	// Create matrix
	Eigen::MatrixXf X = Eigen::MatrixXf::Zero(nVoxels, nFeatures);
	for (unsigned long long i = 0; i < length; ++i) {
		auto feature = allMatrixIonData[i].Feature;
		if (feature >= 0) {
			auto compressedIndex = voxelToIndex[allMatrixIonData[i].Voxel];
			X(compressedIndex, feature) += 1;
		}
	}

	return VoxelFeatureMatrix(std::make_shared<VoxelFeatureMatrixImpl>(std::move(X), std::move(indexToVoxel)));
}
 